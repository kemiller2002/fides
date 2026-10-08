namespace Fides.Client

open System
open System.Security.Cryptography
open System.Text
open Fides

/// Sign-in as pure decisions: starting, reading the callback, and reading the
/// exchange's answer (FID-CLI-001, FID-EXC-001).
module SignIn =
    /// How long a pending sign-in stays valid.
    let pendingLifetime = TimeSpan.FromMinutes 10.

    /// Starts a sign-in from 64 random bytes: 32 for the state, 32 for the
    /// PKCE verifier. Returns what to store for this tab and where to send
    /// the person.
    let start (configuration: ClientConfiguration) (provider: IdentityProvider) (retention: Retention) (random: byte array) (now: DateTimeOffset) =
        if random.Length <> 64 then
            invalidArg (nameof random) "sign-in needs exactly 64 random bytes"

        let state = Pkce.base64Url random[0..31]

        match Pkce.verifierFrom random[32..63] with
        | Error() -> invalidArg (nameof random) "unusable verifier bytes"
        | Ok verifier ->
            let pending =
                { State = state
                  Verifier = verifier
                  CreatedAt = now
                  Retention = retention }

            let url =
                provider.AuthorizationUrl
                    { ClientId = configuration.ClientId
                      RedirectUri = configuration.RedirectUri
                      State = state
                      CodeChallenge = Pkce.challenge verifier }

            pending, url

    /// Compares two states without leaking how much of them matched.
    let private sameState (a: string) (b: string) =
        CryptographicOperations.FixedTimeEquals(ReadOnlySpan(Encoding.UTF8.GetBytes a), ReadOnlySpan(Encoding.UTF8.GetBytes b))

    let private parameter (name: string) (query: (string * string) list) =
        query |> List.tryFind (fun (k, _) -> k = name) |> Option.map snd

    /// What the callback asks for: a refusal, or the exchange request to send.
    type CallbackDecision =
        | Refuse of CallbackOutcome
        | Exchange of Protocol.TokenRequest

    /// Reads the callback against this tab's pending sign-in. The state is
    /// checked before anything else; the pending sign-in is consumed whatever
    /// the answer, so a callback can never be replayed.
    let callback (configuration: ClientConfiguration) (pending: PendingSignIn option) (query: (string * string) list) (now: DateTimeOffset) =
        match pending, parameter "state" query with
        | None, _
        | _, None -> Refuse StateInvalid
        | Some pending, Some state when not (sameState pending.State state) -> Refuse StateInvalid
        | Some pending, Some _ when now - pending.CreatedAt > pendingLifetime || now < pending.CreatedAt -> Refuse StateExpired
        | Some pending, Some _ ->
            match parameter "error" query, parameter "code" query with
            | Some _, _
            | None, None -> Refuse ProviderDenied
            | None, Some code ->
                Exchange
                    { Application = configuration.Application
                      Code = code
                      CodeVerifier = Secret.reveal pending.Verifier
                      RedirectUri = configuration.RedirectUri }

    /// Reads the exchange's answer to `/v1/token`.
    let completed (retention: Retention) (outcome: HttpOutcome) : Result<Session, Protocol.Refusal> =
        match outcome with
        | Failed _ -> Error Protocol.ProviderUnavailable
        | Responded { Status = 200; Body = body } ->
            match Protocol.decodeGrant body with
            | Some(grant, Some identity) -> Ok { Grant = grant; Identity = identity; Retention = retention }
            | _ -> Error Protocol.ProviderContractViolation
        | Responded response -> Error(Protocol.decodeRefusal response.Body |> Option.defaultValue Protocol.ProviderUnavailable)
