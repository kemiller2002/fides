/// The Lambda function in process, with its real HttpClient path pointed at
/// the GitHub simulator: local emulation of the deployed function.
module Fides.Hosting.Aws.Tests.FunctionTests

open System
open System.Net.Http
open System.Threading.Tasks
open Amazon.Lambda.TestUtilities
open Xunit
open Fides
open Fides.Hosting
open Fides.Hosting.Aws
open Fides.Acceptance
open Fides.Acceptance.GitHubSimulator
open Fides.Acceptance.Scenarios

let private ports (handler: HttpMessageHandler) (timeout: TimeSpan) (world: unit -> World) secretAvailable =
    let client = new HttpClient(handler)

    { Send = HttpPort.send client timeout
      ReadSecret = fun _ -> Task.FromResult(if secretAvailable then Ok(Secret.create fixtureClientSecret) else Error())
      Now = fun () -> (world ()).Now }

let private invoke (ports: Ports) (event) =
    let context = TestLambdaContext()
    let logger = TestLambdaLogger()
    context.Logger <- logger
    let aegis, _ = Collecting.aegis ()
    let response = (Function.handle aegis ExchangeFixture.configuration ports event context).GetAwaiter().GetResult()
    response, logger.Buffer.ToString()

let private tokenEvent (code: string) =
    Events.event
        "POST"
        "/v1/token"
        [ "content-type", "application/json"; "origin", "https://chrona.example" ]
        (Some(tokenBody chrona code verifier chrona.RedirectUris.Head))

[<Fact>]
[<Trait("Verifies", "FID-HOST-002")>]
let ``a sign-in through the Lambda function reaches GitHub over HTTP and returns the tokens`` () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)
    let handler = new SimulatorHttpHandler(world)
    let response, log = invoke (ports handler (TimeSpan.FromSeconds 5.) (fun () -> handler.World) true) (tokenEvent context.Code)
    Assert.Equal(200, response.StatusCode)
    Assert.Equal(Some "octocat", JsonRead.object (JsonRead.child "identity" >> Option.bind (JsonRead.string "login")) response.Body)
    Assert.Equal("https://chrona.example", response.Headers["Access-Control-Allow-Origin"])
    Assert.Contains("\"outcome\":\"signed_in\"", log)
    Assert.Contains("\"application\":\"chrona-test\"", log)

[<Fact>]
[<Trait("Verifies", "FID-EXC-005")>]
let ``the function's log carries the audit line and no secret material`` () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)
    let handler = new SimulatorHttpHandler(world)
    let _, log = invoke (ports handler (TimeSpan.FromSeconds 5.) (fun () -> handler.World) true) (tokenEvent context.Code)

    for secret in [ "ghu_sim"; "ghr_sim"; context.Code; fixtureClientSecret; verifier ] do
        Assert.DoesNotContain(secret, log)

[<Fact>]
[<Trait("Verifies", "FID-EXC-004")>]
let ``GitHub unreachable over HTTP is a typed outage`` () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)
    let handler = new SimulatorHttpHandler({ world with Outage = NetworkDown })
    let response, log = invoke (ports handler (TimeSpan.FromSeconds 5.) (fun () -> handler.World) true) (tokenEvent context.Code)
    Assert.Equal(503, response.StatusCode)
    Assert.Equal("""{"error":"provider_unavailable"}""", response.Body)
    Assert.Contains("provider_unavailable", log)

type private SlowHandler() =
    inherit HttpMessageHandler()

    override _.SendAsync(_, cancellationToken) =
        task {
            do! Task.Delay(TimeSpan.FromSeconds 30., cancellationToken)
            return new HttpResponseMessage()
        }

[<Fact>]
[<Trait("Verifies", "FID-EXC-004")>]
let ``GitHub timing out is a typed outage, within the timeout`` () =
    let _, context = signedInAtGitHub chrona octocat (initial epoch)
    let clock = Diagnostics.Stopwatch.StartNew()
    let response, _ = invoke (ports (new SlowHandler()) (TimeSpan.FromMilliseconds 200.) (fun () -> initial epoch) true) (tokenEvent context.Code)
    Assert.Equal(503, response.StatusCode)
    Assert.True(clock.Elapsed < TimeSpan.FromSeconds 10., $"took {clock.Elapsed}")

[<Fact>]
let ``a missing client secret refuses without calling GitHub`` () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)
    let handler = new SimulatorHttpHandler(world)
    let response, _ = invoke (ports handler (TimeSpan.FromSeconds 5.) (fun () -> handler.World) false) (tokenEvent context.Code)
    Assert.Equal(500, response.StatusCode)
    Assert.Empty handler.World.Requests

[<Fact>]
[<Trait("Verifies", "FID-HOST-005")>]
let ``a preflight through the function answers with the exact origin`` () =
    let handler = new SimulatorHttpHandler(initial epoch)
    let event = Events.event "OPTIONS" "/v1/token" [ "origin", "https://summa.example" ] None
    let response, _ = invoke (ports handler (TimeSpan.FromSeconds 5.) (fun () -> handler.World) true) event
    Assert.Equal(204, response.StatusCode)
    Assert.Equal("https://summa.example", response.Headers["Access-Control-Allow-Origin"])

[<Fact>]
let ``the hosting configuration loader reports a missing or invalid document`` () =
    Assert.True(Result.isError (Host.configuration None))
    Assert.True(Result.isError (Host.configuration (Some "{}")))
    Assert.True(Result.isOk (Host.configuration (Some ExchangeFixture.configurationDocument)))
