/// Runs an exchange scenario through any implementation of the service
/// contract: a function from a call to a response, an audit line, and the
/// simulated GitHub afterwards. The exchange core is one implementation; each
/// hosting adapter is another (FID-TEST-002).
module Fides.Tests.Acceptance.ScenarioRunner

open System
open Fides
open Fides.Tests.Acceptance.GitHubSimulator
open Fides.Tests.Acceptance.Scenarios

/// What the scenario runner observes of one answered call.
type Observed =
    { Status: int
      Headers: (string * string) list
      Body: string
      /// Everything the implementation would log for the call.
      Log: string }

/// An implementation under test: answers one call against the simulated
/// world, with the secret store available or not.
type Implementation = bool -> World -> Call -> World * Observed

/// Strings that must never appear in a response or log: the client secret,
/// the PKCE verifiers and the authorization code.
let private neverDisclosed (context: Context) =
    [ fixtureClientSecret; verifier; otherVerifier; context.Code ]

/// Strings that must never appear in a log: any token or code at all.
let private neverLogged = [ "ghu_sim"; "ghr_sim"; "sim-code"; fixtureClientSecret; verifier; otherVerifier ]

/// Runs the scenario; the result lists every way it failed.
let run (implementation: Implementation) (scenario: ExchangeScenario) : string list =
    let world, context = scenario.Arrange(initial epoch)

    let folder (world, context: Context, failures) (index, step: Step) =
        let world = advance step.AdvanceBefore world
        let call = step.Call context
        let world, observed = implementation scenario.SecretAvailable world call
        let at = $"step {index + 1}"

        let expectation =
            match step.Expect with
            | Status status when observed.Status = status -> []
            | Status status -> [ $"{at}: expected HTTP {status}, got {observed.Status} {observed.Body}" ]
            | Refused(status, code) ->
                [ if observed.Status <> status then $"{at}: expected HTTP {status}, got {observed.Status}"
                  if observed.Body <> $"{{\"error\":\"{code}\"}}" then $"{at}: expected refusal {code}, got {observed.Body}" ]

        let headerText = observed.Headers |> List.map (fun (k, v) -> k + ": " + v) |> String.concat "\n"

        let disclosures =
            [ for secret in neverDisclosed context do
                  if observed.Body.Contains secret || headerText.Contains secret then $"{at}: the response discloses a secret" ]

        let logged =
            [ for secret in neverLogged do
                  if observed.Log.Contains secret then $"{at}: the log carries secret material ({secret.Substring(0, 6)}...)" ]

        let context = { context with Responses = context.Responses @ [ observed.Body ] }
        world, context, failures @ expectation @ disclosures @ logged

    let world, context, failures =
        scenario.Steps |> List.indexed |> List.fold folder (world, context, [])

    let providerCalls =
        if not scenario.ProviderCalled && not world.Requests.IsEmpty then
            [ $"GitHub was called {world.Requests.Length} time(s); the refusal must come first" ]
        else
            []

    failures @ providerCalls @ (scenario.Afterwards world context |> Option.toList)

/// The exchange core itself, run with the simulated host.
let core: Implementation =
    fun secretAvailable world call ->
        let world, (response: Service.HostResponse, audit) =
            SimulatedHost.run secretAvailable world (Service.handle ExchangeFixture.configuration (ExchangeFixture.hostRequest call))

        world,
        { Status = response.Status
          Headers = response.Headers
          Body = response.Body
          Log = sprintf "%A" audit }
