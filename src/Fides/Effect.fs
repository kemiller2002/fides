namespace Fides

open System

/// Where the host keeps a client secret: an opaque reference such as an AWS
/// Secrets Manager ARN. The reference is configuration, not a secret.
type SecretReference = SecretReference of string

/// A computation that needs the outside world. Each case is a request for one
/// effect, with the continuation that takes its result. Running it is the
/// host's job (FID-EXC-002); the core only describes it, so every function
/// that returns an `Effect` is pure.
type Effect<'a> =
    | Done of 'a
    | Send of ProviderRequest * (HttpOutcome -> Effect<'a>)
    | ReadSecret of SecretReference * (Result<ClientSecret, unit> -> Effect<'a>)
    | Now of (DateTimeOffset -> Effect<'a>)

module Effect =
    let rec bind (f: 'a -> Effect<'b>) (effect: Effect<'a>) : Effect<'b> =
        match effect with
        | Done value -> f value
        | Send(request, next) -> Send(request, next >> bind f)
        | ReadSecret(reference, next) -> ReadSecret(reference, next >> bind f)
        | Now next -> Now(next >> bind f)

    let map (f: 'a -> 'b) effect = bind (f >> Done) effect

    let send request = Send(request, Done)
    let readSecret reference = ReadSecret(reference, Done)
    let now = Now Done

    /// Run `f` on the success value; a failure short-circuits.
    let bindResult (f: 'a -> Effect<Result<'b, 'e>>) (effect: Effect<Result<'a, 'e>>) =
        effect
        |> bind (function
            | Ok value -> f value
            | Error e -> Done(Error e))

    type Builder() =
        member _.Return value = Done value
        member _.ReturnFrom(effect: Effect<'a>) = effect
        member _.Bind(effect, f) = bind f effect
        member _.Zero() = Done()

    /// `effect { ... }` composes effects.
    let effect = Builder()

    /// Runs an effect with pure handlers, threading a state through them.
    /// Tests use it to drive the core with a simulated provider.
    let rec runWith
        (send: 's -> ProviderRequest -> 's * HttpOutcome)
        (secret: 's -> SecretReference -> Result<ClientSecret, unit>)
        (clock: 's -> DateTimeOffset)
        (state: 's)
        (effect: Effect<'a>)
        : 's * 'a =
        match effect with
        | Done value -> state, value
        | Send(request, next) ->
            let state, outcome = send state request
            runWith send secret clock state (next outcome)
        | ReadSecret(reference, next) -> runWith send secret clock state (next (secret state reference))
        | Now next -> runWith send secret clock state (next (clock state))
