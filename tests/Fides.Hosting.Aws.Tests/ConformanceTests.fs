/// The AWS adapter passes the shared hosting-adapter conformance suite
/// (FID-TEST-002): driven through API Gateway events and the Lambda handler,
/// with real HttpClient calls to simulated GitHub, it satisfies every exchange
/// acceptance scenario and answers exactly as the exchange core does.
module Fides.Hosting.Aws.Tests.ConformanceTests

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
open Fides.Acceptance.ScenarioRunner

/// The AWS adapter as a conformance implementation.
let aws: Implementation =
    fun secretAvailable world call ->
        use handler = new SimulatorHttpHandler(world)
        use client = new HttpClient(handler, disposeHandler = false)

        let ports =
            { Send = HttpPort.send client (TimeSpan.FromSeconds 5.)
              ReadSecret = fun _ -> Task.FromResult(if secretAvailable then Ok(Secret.create fixtureClientSecret) else Error())
              Now = fun () -> handler.World.Now }

        // API Gateway delivers header names in lower case.
        let headers =
            [ "content-type", "application/json" ]
            @ (call.Origin |> Option.map (fun o -> [ "origin", o ]) |> Option.defaultValue [])

        let event =
            Events.read (Events.json call.Method call.Path "$default" headers (if call.Body = "" then None else Some call.Body) false)

        let context = TestLambdaContext()
        let logger = TestLambdaLogger()
        context.Logger <- logger
        let response = (Function.handle ExchangeFixture.configuration ports event context).GetAwaiter().GetResult()

        handler.World,
        { Status = response.StatusCode
          Headers = [ for KeyValue(k, v) in response.Headers -> k, v ]
          Body = string response.Body
          Log = logger.Buffer.ToString() }

let scenarioIds: seq<objnull array> = exchangeScenarios |> Seq.map (fun s -> [| box s.Id |])
let caseIds: seq<objnull array> = Conformance.extraCases |> Seq.map (fun (id, _) -> [| box id |])

[<Theory>]
[<MemberData(nameof scenarioIds)>]
[<Trait("Verifies", "FID-TEST-002")>]
[<Trait("Verifies", "FID-HOST-004")>]
let ``the AWS adapter conforms on the acceptance scenario`` (id: string) =
    let scenario = exchangeScenarios |> List.find (fun s -> s.Id = id)
    Assert.Empty(Conformance.scenarioFailures core aws scenario)

[<Theory>]
[<MemberData(nameof caseIds)>]
[<Trait("Verifies", "FID-TEST-002")>]
let ``the AWS adapter answers the conformance case exactly as the core does`` (id: string) =
    let _, call = Conformance.extraCases |> List.find (fun (caseId, _) -> caseId = id)
    Assert.Empty(Conformance.caseFailures core aws call)
