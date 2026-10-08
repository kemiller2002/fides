namespace Fides.Arca

open Fides
open Fides.Client

/// Fides's client as Arca's token provider (ARCA-AUTH-001). The two failure
/// models are the same four cases (DF-FIDES-2026-0008), so the bridge only
/// renames them.
module TokenBridge =
    /// Fides's reason as Arca's.
    let unavailable (reason: TokenUnavailable) : Arca.TokenUnavailable =
        match reason with
        | TokenUnavailable.NoToken -> Arca.TokenUnavailable.NoToken
        | TokenUnavailable.Expired -> Arca.TokenUnavailable.Expired
        | TokenUnavailable.Revoked -> Arca.TokenUnavailable.Revoked
        | TokenUnavailable.ProviderFailed reason -> Arca.TokenUnavailable.ProviderFailed reason

    /// Fides's token as Arca's. A value Arca refuses (empty, or holding
    /// whitespace or control characters) is a provider failure, and the value
    /// itself is not repeated in the reason.
    let token (value: AccessToken) : Result<Arca.AccessToken, Arca.TokenUnavailable> =
        match Arca.AccessToken.create (Secret.reveal value) with
        | Ok token -> Ok token
        | Error _ -> Error(Arca.TokenUnavailable.ProviderFailed "the token is not one Arca accepts")

    /// Arca's token provider, backed by a Fides token provider.
    let toArca (provider: Fides.Client.TokenProvider) : Arca.TokenProvider =
        fun () ->
            async {
                match! provider () with
                | Ok value -> return token value
                | Error reason -> return Error(unavailable reason)
            }

    /// Arca's token provider for a signed-in Fides client.
    let ofClient (client: FidesClient) : Arca.TokenProvider = toArca client.TokenProvider
