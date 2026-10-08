namespace Fides.Hosting

open System
open System.Collections.Concurrent
open System.Threading.Tasks
open Fides

/// Keeps a secret read for a short time, so a warm host does not call its
/// secret store on every request. Only successful reads are kept; a rotated
/// secret is picked up within the time-to-live.
module SecretCache =
    let cached (timeToLive: TimeSpan) (now: unit -> DateTimeOffset) (read: SecretReference -> Task<Result<ClientSecret, unit>>) =
        let entries = ConcurrentDictionary<SecretReference, ClientSecret * DateTimeOffset>()

        fun (reference: SecretReference) ->
            task {
                match entries.TryGetValue reference with
                | true, (secret, expires) when now () < expires -> return Ok secret
                | _ ->
                    let! result = read reference

                    match result with
                    | Ok secret -> entries[reference] <- (secret, now () + timeToLive)
                    | Error() -> entries.TryRemove reference |> ignore

                    return result
            }
