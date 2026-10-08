/// The conformance suite itself: platform-free, and able to catch an adapter
/// that changes anything observable (FID-TEST-002, FID-HOST-004).
module Fides.Tests.ConformanceSuiteTests

open Xunit
open Fides.Acceptance
open Fides.Acceptance.Scenarios
open Fides.Acceptance.ScenarioRunner

let private scenario id = exchangeScenarios |> List.find (fun s -> s.Id = id)

let private altered (change: Observed -> Observed) : Implementation =
    fun secret world call ->
        let world, observed = core secret world call
        world, change observed

[<Fact>]
[<Trait("Verifies", "FID-HOST-004")>]
let ``the suite references no hosting platform, so any adapter can run it`` () =
    let references =
        typeof<Fides.Acceptance.ScenarioRunner.Observed>.Assembly.GetReferencedAssemblies()
        |> Array.choose (fun a -> a.Name |> Option.ofObj)

    Assert.DoesNotContain(references, fun name -> name.StartsWith "Amazon" || name.StartsWith "AWSSDK" || name.StartsWith "Azure" || name.StartsWith "Microsoft.Azure" || name.StartsWith "Fides.Hosting")

[<Fact>]
let ``the core conforms to itself`` () =
    for s in exchangeScenarios do
        Assert.Empty(Conformance.scenarioFailures core core s)

    for _, call in Conformance.extraCases do
        Assert.Empty(Conformance.caseFailures core core call)

[<Fact>]
[<Trait("Verifies", "FID-TEST-002")>]
let ``an adapter that drops CORS headers does not conform`` () =
    let dropsCors = altered (fun o -> { o with Headers = o.Headers |> List.filter (fun (k, _) -> not (k.StartsWith "Access-Control")) })
    Assert.NotEmpty(Conformance.scenarioFailures core dropsCors (scenario "sign-in-succeeds"))

[<Fact>]
[<Trait("Verifies", "FID-TEST-002")>]
let ``an adapter that adds a header does not conform`` () =
    let adds = altered (fun o -> { o with Headers = ("X-Powered-By", "host") :: o.Headers })
    Assert.NotEmpty(Conformance.caseFailures core adds (snd Conformance.extraCases.Head))

[<Fact>]
[<Trait("Verifies", "FID-TEST-002")>]
let ``an adapter that rewrites a status does not conform even when the scenario would pass`` () =
    let rewrites = altered (fun o -> if o.Status = 404 then { o with Status = 400 } else o)
    let call = Conformance.extraCases |> List.find (fun (id, _) -> id = "get-unknown-path") |> snd
    Assert.NotEmpty(Conformance.caseFailures core rewrites call)

[<Fact>]
let ``header names are compared without case`` () =
    let lowercases = altered (fun o -> { o with Headers = o.Headers |> List.map (fun (k, v) -> k.ToLowerInvariant(), v) })
    Assert.Empty(Conformance.scenarioFailures core lowercases (scenario "sign-in-succeeds"))
