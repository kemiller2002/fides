/// Unexpected failures at the exchange host are classified through Aegis,
/// and nothing secret reaches an Aegis sink (FID-EXC-005).
module Fides.Hosting.Aws.Tests.DiagnosticsTests

open System
open System.Net.Http
open System.Threading.Tasks
open Xunit
open Fides
open Fides.Hosting
open Fides.Acceptance
open Fides.Acceptance.GitHubSimulator
open Fides.Acceptance.Scenarios

let private request (code: string) : Service.HostRequest =
    { Method = "POST"
      Path = Protocol.TokenPath
      Headers = [ "Content-Type", "application/json"; "Origin", "https://chrona.example" ]
      Body = tokenBody chrona code verifier chrona.RedirectUris.Head }

let private run (ports: Ports) (hostRequest: Service.HostRequest) =
    let aegis, events = Collecting.aegis ()
    let response, line = (Host.handle aegis ExchangeFixture.configuration ports hostRequest).GetAwaiter().GetResult()
    response, line, List.ofSeq events

let private simulated (world: World) secretAvailable =
    let handler = new SimulatorHttpHandler(world)
    let client = new HttpClient(handler)

    handler,
    { Send = HttpPort.send client (TimeSpan.FromSeconds 5.)
      ReadSecret = fun _ -> Task.FromResult(if secretAvailable then Ok(Secret.create fixtureClientSecret) else Error())
      Now = fun () -> handler.World.Now }

let private secretMaterial (code: string) =
    [ fixtureClientSecret; verifier; code; "ghu_sim"; "ghr_sim" ]

[<Fact>]
[<Trait("Verifies", "FID-EXC-005")>]
let ``an exception escaping the exchange is an Aegis fault and an internal_error that discloses nothing`` () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)
    let _, healthy = simulated world true

    let throwing =
        { healthy with
            ReadSecret = fun _ -> raise (InvalidOperationException $"store failed while handling code {context.Code} and {fixtureClientSecret}") }

    let response, line, events = run throwing (request context.Code)
    Assert.Equal(500, response.Status)
    Assert.Equal("""{"error":"internal_error"}""", response.Body)
    Assert.DoesNotContain(response.Headers, fun (k, _) -> k.StartsWith "Access-Control")
    Assert.Contains("\"outcome\":\"internal_error\"", line)
    let event = Assert.Single events
    Assert.Contains("FIDES.HOST.UNEXPECTED", event)
    Assert.True(event.Contains "System.InvalidOperationException", event)

    for secret in secretMaterial context.Code do
        Assert.DoesNotContain(secret, event)
        Assert.DoesNotContain(secret, line)

[<Fact>]
[<Trait("Verifies", "FID-EXC-005")>]
let ``a provider outage is reported to Aegis and still answered as provider_unavailable`` () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)
    let _, ports = simulated { world with Outage = TokenEndpointDown } true
    let response, _, events = run ports (request context.Code)
    Assert.Equal(503, response.Status)
    let event = Assert.Single events
    Assert.Contains("FIDES.PROVIDER.UNAVAILABLE", event)

    for secret in secretMaterial context.Code do
        Assert.DoesNotContain(secret, event)

[<Fact>]
let ``an unreadable client secret is reported to Aegis as a configuration fault`` () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)
    let _, ports = simulated world false
    let response, _, events = run ports (request context.Code)
    Assert.Equal(500, response.Status)
    Assert.Equal("""{"error":"configuration_unavailable"}""", response.Body)
    Assert.Contains("FIDES.CONFIGURATION.UNAVAILABLE", Assert.Single events)

[<Fact>]
let ``a provider contract violation is reported to Aegis`` () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)
    let _, ports = simulated { world with ExpiringTokens = false } true
    let _, _, events = run ports (request context.Code)
    Assert.Contains("FIDES.PROVIDER.CONTRACT_VIOLATION", Assert.Single events)

[<Fact>]
let ``a successful sign-in and an ordinary refusal report nothing to Aegis`` () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)
    let _, ports = simulated world true
    let response, _, events = run ports (request context.Code)
    Assert.Equal(200, response.Status)
    Assert.Empty events
    let _, _, refused = run ports (request "already-used")
    Assert.Empty refused
