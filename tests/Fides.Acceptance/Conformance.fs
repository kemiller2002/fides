/// The hosting-adapter conformance suite (FID-TEST-002, FID-HOST-004). An
/// adapter conforms when, driven through its own platform's request and
/// response shapes, it
///
/// 1. satisfies every exchange acceptance scenario, and
/// 2. behaves identically to the exchange core itself: for every step of every
///    scenario and every extra conformance case, the same status, body and
///    headers (header names compared without case).
///
/// The suite knows no platform. A new adapter (Azure Functions, a container)
/// supplies one `ScenarioRunner.Implementation` and runs `failures`.
module Fides.Acceptance.Conformance

open System
open Fides.Acceptance.GitHubSimulator
open Fides.Acceptance.Scenarios
open Fides.Acceptance.ScenarioRunner

/// Requests beyond the scenarios that exercise the HTTP surface: header-name
/// case, content-type parameters, preflights, unknown paths and methods,
/// empty and oversized bodies.
let extraCases: (string * Call) list =
    let token = tokenBody chrona "sim-code-0001" verifier chrona.RedirectUris.Head

    [ "preflight-registered", { Method = "OPTIONS"; Path = "/v1/token"; Origin = Some "https://chrona.example"; Body = "" }
      "preflight-other-app", { Method = "OPTIONS"; Path = "/v1/refresh"; Origin = Some "https://summa.example"; Body = "" }
      "preflight-unregistered", { Method = "OPTIONS"; Path = "/v1/revoke"; Origin = Some "https://attacker.example"; Body = "" }
      "preflight-no-origin", { Method = "OPTIONS"; Path = "/v1/token"; Origin = None; Body = "" }
      "preflight-unknown-path", { Method = "OPTIONS"; Path = "/v2/token"; Origin = Some "https://chrona.example"; Body = "" }
      "get-unknown-path", { Method = "GET"; Path = "/"; Origin = None; Body = "" }
      "delete-known-path", { Method = "DELETE"; Path = "/v1/token"; Origin = Some "https://chrona.example"; Body = "" }
      "lowercase-method", { Method = "post"; Path = "/v1/token"; Origin = Some "https://chrona.example"; Body = token }
      "empty-body", { Method = "POST"; Path = "/v1/token"; Origin = Some "https://chrona.example"; Body = "" }
      "json-array-body", { Method = "POST"; Path = "/v1/refresh"; Origin = Some "https://chrona.example"; Body = "[]" }
      "unicode-body", { Method = "POST"; Path = "/v1/revoke"; Origin = Some "https://chrona.example"; Body = """{"application":"chrona-test","accessToken":"tökén✓"}""" }
      "wrong-redirect", { Method = "POST"; Path = "/v1/token"; Origin = Some "https://chrona.example"; Body = tokenBody chrona "c" verifier "https://chrona.example/" } ]

let private normalise (observed: Observed) =
    observed.Status,
    observed.Body,
    observed.Headers |> List.map (fun (k, v) -> k.ToLowerInvariant(), v) |> List.sort

/// Where the candidate's observable behaviour differs from the reference's,
/// replaying the same calls from the same world through both.
let differences (reference: Implementation) (candidate: Implementation) (secretAvailable: bool) (arrange: World -> World * Context) (calls: (TimeSpan * (Context -> Call)) list) =
    let world, context = arrange (initial epoch)

    let folder (referenceWorld, candidateWorld, context: Context, found) (index, (advanceBy: TimeSpan, call: Context -> Call)) =
        let referenceWorld = advance advanceBy referenceWorld
        let candidateWorld = advance advanceBy candidateWorld
        let request = call context
        let referenceWorld, expected = reference secretAvailable referenceWorld request
        let candidateWorld, actual = candidate secretAvailable candidateWorld request

        let found =
            if normalise expected = normalise actual then
                found
            else
                found @ [ $"call {index + 1} ({request.Method} {request.Path}): reference {expected.Status} {expected.Body}, adapter {actual.Status} {actual.Body}" ]

        referenceWorld, candidateWorld, { context with Responses = context.Responses @ [ expected.Body ] }, found

    let _, _, _, found = calls |> List.indexed |> List.fold folder (world, world, context, [])
    found

/// Every way the candidate fails a scenario: the scenario's own checks, and
/// any difference from the reference.
let scenarioFailures (reference: Implementation) (candidate: Implementation) (scenario: ExchangeScenario) =
    let own = run candidate scenario
    let calls = scenario.Steps |> List.map (fun step -> step.AdvanceBefore, step.Call)
    own @ differences reference candidate scenario.SecretAvailable scenario.Arrange calls

/// Every way the candidate differs from the reference on an extra case.
let caseFailures (reference: Implementation) (candidate: Implementation) (call: Call) =
    differences reference candidate true (fun world -> world, { Code = "sim-code-0001"; Responses = [] }) [ TimeSpan.Zero, fun _ -> call ]
