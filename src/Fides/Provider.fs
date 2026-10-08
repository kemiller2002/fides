namespace Fides

open System

/// Identifies an identity provider, such as `github`.
type ProviderId = ProviderId of string

/// What a provider can do. A provider declares its capabilities so that
/// callers can depend on them explicitly (FID-PRV-001).
type ProviderCapability =
    /// Authorization-code flow with PKCE S256.
    | AuthorizationCodeWithPkce
    /// Access tokens expire and come with a refresh token.
    | ExpiringTokens
    /// Refresh tokens can be exchanged for a new pair.
    | RefreshTokens
    /// Tokens can be revoked through the provider.
    | Revocation
    /// The provider can tell whether a token can read a repository.
    | RepositoryAccess

/// Who the person is, as the provider reports it for a token (FID-PRV-003).
/// `Subject` is the provider's stable account id; `Login` is for display.
type Identity =
    { Provider: ProviderId
      Subject: string
      Login: string
      Name: string option }

/// Tokens the provider issued, with absolute expiry instants.
type TokenGrant =
    { AccessToken: AccessToken
      AccessTokenExpiresAt: DateTimeOffset
      RefreshToken: RefreshToken
      RefreshTokenExpiresAt: DateTimeOffset }

/// The exchange's OAuth client credentials for one provider.
type ClientCredentials =
    { ClientId: string
      ClientSecret: ClientSecret }

/// What the client sends the person to the provider with.
type AuthorizationRequest =
    { ClientId: string
      RedirectUri: string
      State: string
      CodeChallenge: string }

/// A repository the provider hosts, as `owner/name`.
type RepositoryName = private RepositoryName of string

module RepositoryName =
    let private valid (part: string) =
        part.Length > 0
        && part.Length <= 100
        && part <> "."
        && part <> ".."
        && part |> Seq.forall (fun c -> Char.IsAsciiLetterOrDigit c || c = '-' || c = '_' || c = '.')

    /// `owner/name` with GitHub's characters only, so it can never alter the
    /// path it is placed in.
    let parse (value: string) =
        match value.Split '/' with
        | [| owner; name |] when valid owner && valid name -> Ok(RepositoryName value)
        | _ -> Error()

    let value (RepositoryName name) = name

/// Whether a token can read a repository.
type RepositoryAccessResult =
    | Readable
    | NotReadable

/// How a provider answered, once interpreted. Provider text never passes
/// through: only these cases leave the provider module.
type ProviderRefusal =
    /// The code was wrong, expired, already used or did not match the verifier.
    | CodeRejected
    /// The refresh token was revoked, expired or already used.
    | RefreshRejected
    /// The access token is no longer accepted.
    | TokenRejected
    /// The provider rejected the exchange's own client credentials.
    | ClientCredentialsRejected
    /// The provider failed, timed out or rate-limited.
    | Unavailable
    /// The provider answered in a shape Fides does not accept.
    | ContractViolation of reason: string

/// An identity provider: pure functions that build its requests and read its
/// responses (FID-PRV-001). Running the requests is the host's job.
type IdentityProvider =
    { Id: ProviderId
      Capabilities: Set<ProviderCapability>
      /// What the provider's authentication proves (DF-FIDES-2026-0007). A
      /// provider must state it before it is added.
      Proves: string list
      AuthorizationUrl: AuthorizationRequest -> string
      CodeExchange: ClientCredentials -> AuthorizationCode -> CodeVerifier -> string -> ProviderRequest
      Refresh: ClientCredentials -> RefreshToken -> ProviderRequest
      /// Reads a token response received at the given instant.
      ReadTokenGrant: DateTimeOffset -> HttpOutcome -> Result<TokenGrant, ProviderRefusal>
      IdentityRequest: AccessToken -> ProviderRequest
      ReadIdentity: HttpOutcome -> Result<Identity, ProviderRefusal>
      Revocation: ClientCredentials -> AccessToken -> ProviderRequest
      ReadRevocation: HttpOutcome -> Result<unit, ProviderRefusal>
      RepositoryAccessRequest: AccessToken -> RepositoryName -> ProviderRequest
      ReadRepositoryAccess: HttpOutcome -> Result<RepositoryAccessResult, ProviderRefusal> }

/// Provider operations as effects, the same for every provider.
module Provider =
    open Effect

    let private withNow (read: DateTimeOffset -> HttpOutcome -> 'a) request =
        effect {
            let! outcome = send request
            let! now = now
            return read now outcome
        }

    /// Exchange an authorization code for tokens.
    let exchangeCode (provider: IdentityProvider) credentials code verifier redirectUri =
        provider.CodeExchange credentials code verifier redirectUri |> withNow provider.ReadTokenGrant

    /// Exchange a refresh token for a new pair.
    let refresh (provider: IdentityProvider) credentials refreshToken =
        provider.Refresh credentials refreshToken |> withNow provider.ReadTokenGrant

    /// Resolve who the token belongs to.
    let identify (provider: IdentityProvider) accessToken =
        provider.IdentityRequest accessToken |> send |> map provider.ReadIdentity

    /// Revoke a token at the provider.
    let revoke (provider: IdentityProvider) credentials accessToken =
        provider.Revocation credentials accessToken |> send |> map provider.ReadRevocation

    /// Whether the token can read a repository.
    let repositoryAccess (provider: IdentityProvider) accessToken repository =
        provider.RepositoryAccessRequest accessToken repository |> send |> map provider.ReadRepositoryAccess

/// The providers an exchange or client can use, by id. Adding a provider adds
/// an entry here and changes nothing in the applications (FID-PRV-004).
type ProviderCatalog = Map<ProviderId, IdentityProvider>

module ProviderCatalog =
    let ofList (providers: IdentityProvider list) : ProviderCatalog =
        providers |> List.map (fun p -> p.Id, p) |> Map.ofList

    let tryFind (id: ProviderId) (catalog: ProviderCatalog) = catalog.TryFind id
