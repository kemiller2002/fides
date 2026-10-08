namespace Fides.Client

open System
open Fides

/// The person's explicit agreement to keep tokens across browser restarts,
/// given after the application showed them what that means (FID-CLI-002). It
/// can only be made from the disclosure text the person saw.
type PersistenceConsent = private PersistenceConsent of disclosure: string

module PersistenceConsent =
    /// Consent recorded with the disclosure the person was shown.
    let givenAfter (disclosure: string) =
        if String.IsNullOrWhiteSpace disclosure then Error() else Ok(PersistenceConsent disclosure)

    let disclosure (PersistenceConsent text) = text

/// Where the session's tokens live (FID-CLI-002).
type Retention =
    /// In memory only; gone on reload. The default.
    | MemoryOnly
    /// In this tab's session storage; gone when the tab closes.
    | SessionScoped
    /// In local storage, across restarts; only with the person's consent.
    | Persistent of PersistenceConsent

/// How an application is set up to use Fides. Nothing here is secret.
type ClientConfiguration =
    { /// The application's id as registered with the exchange.
      Application: string
      /// The identity provider, by id; changing it is configuration only (FID-PRV-004).
      Provider: ProviderId
      /// The exchange's OAuth client id at the provider. Public.
      ClientId: string
      /// The exact redirect URI registered for this application.
      RedirectUri: string }

/// A sign-in this tab started: kept in the tab's session storage across the
/// navigation to the provider, consumed by the first callback.
type PendingSignIn =
    { State: string
      Verifier: CodeVerifier
      CreatedAt: DateTimeOffset
      Retention: Retention }

/// A signed-in session.
type Session =
    { Grant: TokenGrant
      Identity: Identity
      Retention: Retention }

/// The session as applications see it (FID-CLI-003).
type SessionState =
    | SignedOut
    | SigningIn
    | SignedIn of Identity
    | Expired
    | Revoked
    | ProviderUnavailable

module SessionState =
    let code =
        function
        | SignedOut -> "signed_out"
        | SigningIn -> "signing_in"
        | SignedIn _ -> "signed_in"
        | Expired -> "expired"
        | Revoked -> "revoked"
        | ProviderUnavailable -> "provider_unavailable"

/// Why the token provider has no usable token. The cases mirror Arca's
/// token-provider port (`Arca.TokenUnavailable`, ARCA-AUTH-001) one for one.
[<RequireQualifiedAccess>]
type TokenUnavailable =
    | NoToken
    | Expired
    | Revoked
    /// The exchange or provider failed; the reason holds no secret.
    | ProviderFailed of reason: string

module TokenUnavailable =
    let code =
        function
        | TokenUnavailable.NoToken -> "none"
        | TokenUnavailable.Expired -> "expired"
        | TokenUnavailable.Revoked -> "revoked"
        | TokenUnavailable.ProviderFailed _ -> "provider_unavailable"

/// The token-provider function Arca's port expects, in Fides's types.
type TokenProvider = unit -> Async<Result<AccessToken, TokenUnavailable>>

/// How a callback ended (EXCHANGE-PROTOCOL.md section 5).
type CallbackOutcome =
    | CompletedSignIn of Identity
    | StateInvalid
    | StateExpired
    | ProviderDenied
    | ExchangeRefused of Protocol.Refusal

module CallbackOutcome =
    let code =
        function
        | CompletedSignIn _ -> "signed_in"
        | StateInvalid -> "state_invalid"
        | StateExpired -> "state_expired"
        | ProviderDenied -> "provider_denied"
        | ExchangeRefused refusal -> Protocol.code refusal
