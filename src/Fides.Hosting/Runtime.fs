namespace Fides.Hosting

open System
open System.Threading.Tasks
open Fides

/// The effects a host must provide to run the exchange core.
type Ports =
    { /// Sends a provider request. Must not throw: failures are `Failed`.
      Send: ProviderRequest -> Task<HttpOutcome>
      /// Reads a client secret by reference. Must not throw.
      ReadSecret: SecretReference -> Task<Result<ClientSecret, unit>>
      Now: unit -> DateTimeOffset }

/// Runs the pure core's effects against real ports.
module Runtime =
    let rec run (ports: Ports) (effect: Effect<'a>) : Task<'a> =
        task {
            match effect with
            | Done value -> return value
            | Send(request, next) ->
                let! outcome = ports.Send request
                return! run ports (next outcome)
            | ReadSecret(reference, next) ->
                let! secret = ports.ReadSecret reference
                return! run ports (next secret)
            | Now next -> return! run ports (next (ports.Now()))
        }
