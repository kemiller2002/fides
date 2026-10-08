/// Runs the core's effects against the GitHub simulator: the test stand-in
/// for a hosting adapter's interpreter.
module Fides.Tests.Acceptance.SimulatedHost

open System
open Fides
open Fides.Tests.Acceptance.GitHubSimulator

let private formEncode (pairs: (string * string) list) =
    pairs |> List.map (fun (k, v) -> Uri.EscapeDataString k + "=" + Uri.EscapeDataString v) |> String.concat "&"

/// The wire form of a provider request.
let toSimRequest (request: ProviderRequest) : SimRequest =
    match request.Body with
    | NoBody -> { Method = request.Method; Url = request.Url; Headers = request.Headers; Body = "" }
    | Form pairs ->
        { Method = request.Method
          Url = request.Url
          Headers = request.Headers @ [ "Content-Type", "application/x-www-form-urlencoded" ]
          Body = formEncode pairs }
    | Json text ->
        { Method = request.Method
          Url = request.Url
          Headers = request.Headers @ [ "Content-Type", "application/json" ]
          Body = text }

let send (world: World) (request: ProviderRequest) =
    let world, outcome = handle world (toSimRequest request)

    world,
    match outcome with
    | SimOutcome.Responded(status, body) -> HttpOutcome.Responded { Status = status; Body = body }
    | SimOutcome.Unreachable -> HttpOutcome.Failed TransportFailure.Unreachable

/// Runs an effect against the simulated world. The secret store holds the
/// fixture client secret when `secretAvailable`.
let run (secretAvailable: bool) (world: World) (effect: Effect<'a>) =
    Effect.runWith
        send
        (fun _ _ -> if secretAvailable then Ok(Secret.create fixtureClientSecret) else Error())
        (fun world -> world.Now)
        world
        effect
