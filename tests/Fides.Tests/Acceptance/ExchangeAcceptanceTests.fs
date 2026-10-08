/// Every exchange acceptance scenario from WI-0005, run against the exchange
/// core (FID-TEST-001).
module Fides.Tests.Acceptance.ExchangeAcceptanceTests

open Xunit
open Fides.Tests.Acceptance.Scenarios

let scenarioIds: seq<objnull array> = exchangeScenarios |> Seq.map (fun s -> [| box s.Id |])

[<Theory>]
[<MemberData(nameof scenarioIds)>]
[<Trait("Verifies", "FID-TEST-001")>]
[<Trait("Verifies", "FID-EXC-001")>]
[<Trait("Verifies", "FID-EXC-003")>]
[<Trait("Verifies", "FID-EXC-004")>]
[<Trait("Verifies", "FID-EXC-005")>]
[<Trait("Verifies", "FID-TB-003")>]
[<Trait("Verifies", "FID-TB-004")>]
[<Trait("Verifies", "FID-TB-005")>]
[<Trait("Verifies", "FID-HOST-005")>]
let ``the exchange core satisfies the acceptance scenario`` (id: string) =
    let scenario = exchangeScenarios |> List.find (fun s -> s.Id = id)
    Assert.Empty(ScenarioRunner.run ScenarioRunner.core scenario)

/// The runner must fail implementations that are wrong, leaky or call the
/// provider too early, or a passing scenario would prove nothing.
module RunnerSelfTests =
    open Fides.Tests.Acceptance.GitHubSimulator
    open Fides.Tests.Acceptance.ScenarioRunner

    let private scenario id = exchangeScenarios |> List.find (fun s -> s.Id = id)

    [<Fact>]
    let ``an implementation that accepts everything fails the refusal scenarios`` () =
        let acceptsAll: Implementation =
            fun _ world _ -> world, { Status = 200; Headers = []; Body = "{}"; Log = "" }

        Assert.NotEmpty(run acceptsAll (scenario "wrong-callback"))
        Assert.NotEmpty(run acceptsAll (scenario "provider-outage"))

    [<Fact>]
    let ``an implementation that echoes the request fails on disclosure`` () =
        let echoes: Implementation =
            fun secret world call ->
                let world, observed = core secret world call
                world, { observed with Body = observed.Body + call.Body }

        Assert.Contains(run echoes (scenario "sign-in-succeeds"), fun f -> f.Contains "discloses")

    [<Fact>]
    let ``an implementation that logs the response fails on log hygiene`` () =
        let logs: Implementation =
            fun secret world call ->
                let world, observed = core secret world call
                world, { observed with Log = observed.Body }

        Assert.Contains(run logs (scenario "sign-in-succeeds"), fun f -> f.Contains "log carries")

    [<Fact>]
    let ``an implementation that calls GitHub before validating fails`` () =
        let eager: Implementation =
            fun secret world call ->
                let world, _ = handle world { Method = "GET"; Url = "https://api.github.com/user"; Headers = []; Body = "" }
                core secret world call

        Assert.Contains(run eager (scenario "wrong-callback"), fun f -> f.Contains "GitHub was called")
