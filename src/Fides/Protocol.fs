namespace Fides

open System
open System.Globalization

/// The exchange's public wire contract (docs/architecture/EXCHANGE-PROTOCOL.md),
/// shared by the exchange and the client so both read and write it the same way.
module Protocol =
    /// Every refusal the exchange can answer with. The codes are the contract.
    type Refusal =
        | MalformedRequest
        | UnknownApplication
        | OriginNotAllowed
        | RedirectUriNotAllowed
        | InvalidCodeVerifier
        | ConfigurationUnavailable
        | CodeRejected
        | RefreshRejected
        | IdentityRevoked
        | RepositoryAccessDenied
        | ProviderUnavailable
        | ProviderContractViolation
        | NotFound
        | MethodNotAllowed

    let allRefusals =
        [ MalformedRequest; UnknownApplication; OriginNotAllowed; RedirectUriNotAllowed; InvalidCodeVerifier
          ConfigurationUnavailable; CodeRejected; RefreshRejected; IdentityRevoked; RepositoryAccessDenied
          ProviderUnavailable; ProviderContractViolation; NotFound; MethodNotAllowed ]

    let code =
        function
        | MalformedRequest -> "malformed_request"
        | UnknownApplication -> "unknown_application"
        | OriginNotAllowed -> "origin_not_allowed"
        | RedirectUriNotAllowed -> "redirect_uri_not_allowed"
        | InvalidCodeVerifier -> "invalid_code_verifier"
        | ConfigurationUnavailable -> "configuration_unavailable"
        | CodeRejected -> "code_rejected"
        | RefreshRejected -> "refresh_rejected"
        | IdentityRevoked -> "identity_revoked"
        | RepositoryAccessDenied -> "repository_access_denied"
        | ProviderUnavailable -> "provider_unavailable"
        | ProviderContractViolation -> "provider_contract_violation"
        | NotFound -> "not_found"
        | MethodNotAllowed -> "method_not_allowed"

    let status =
        function
        | MalformedRequest
        | UnknownApplication
        | RedirectUriNotAllowed
        | InvalidCodeVerifier
        | CodeRejected -> 400
        | RefreshRejected
        | IdentityRevoked -> 401
        | OriginNotAllowed
        | RepositoryAccessDenied -> 403
        | NotFound -> 404
        | MethodNotAllowed -> 405
        | ConfigurationUnavailable -> 500
        | ProviderContractViolation -> 502
        | ProviderUnavailable -> 503

    let ofCode (value: string) = allRefusals |> List.tryFind (fun r -> code r = value)

    /// The largest request body the exchange reads, in UTF-8 bytes.
    [<Literal>]
    let MaxBodyBytes = 8192

    let TokenPath = "/v1/token"
    let RefreshPath = "/v1/refresh"
    let RevokePath = "/v1/revoke"

    /// `POST /v1/token`.
    type TokenRequest =
        { Application: string
          Code: string
          CodeVerifier: string
          RedirectUri: string }

    /// `POST /v1/refresh`.
    type RefreshRequest = { Application: string; RefreshToken: string }

    /// `POST /v1/revoke`.
    type RevokeRequest = { Application: string; AccessToken: string }

    open JsonWrite

    let private text (name: string) root = JsonRead.string name root |> Option.filter (fun v -> v.Length > 0)

    let encodeTokenRequest (r: TokenRequest) =
        render (Object [ "application", String r.Application; "code", String r.Code; "codeVerifier", String r.CodeVerifier; "redirectUri", String r.RedirectUri ])

    let decodeTokenRequest (body: string) =
        body
        |> JsonRead.object (fun root ->
            match text "application" root, text "code" root, text "codeVerifier" root, text "redirectUri" root with
            | Some application, Some code, Some verifier, Some redirect ->
                Some { Application = application; Code = code; CodeVerifier = verifier; RedirectUri = redirect }
            | _ -> None)

    let encodeRefreshRequest (r: RefreshRequest) =
        render (Object [ "application", String r.Application; "refreshToken", String r.RefreshToken ])

    let decodeRefreshRequest (body: string) =
        body
        |> JsonRead.object (fun root ->
            match text "application" root, text "refreshToken" root with
            | Some application, Some token -> Some { Application = application; RefreshToken = token }
            | _ -> None)

    let encodeRevokeRequest (r: RevokeRequest) =
        render (Object [ "application", String r.Application; "accessToken", String r.AccessToken ])

    let decodeRevokeRequest (body: string) =
        body
        |> JsonRead.object (fun root ->
            match text "application" root, text "accessToken" root with
            | Some application, Some token -> Some { Application = application; AccessToken = token }
            | _ -> None)

    /// The application named by a request body, if it names one.
    let application (body: string) = body |> JsonRead.object (text "application")

    /// Instants travel as UTC ISO 8601 with a `Z`.
    let formatInstant (instant: DateTimeOffset) =
        instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)

    let parseInstant (value: string) =
        match DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal) with
        | true, instant -> Some instant
        | _ -> None

    /// A token response: the grant, and the identity on `/v1/token`.
    let encodeGrant (grant: TokenGrant) (identity: Identity option) =
        render (
            objectOf
                [ "accessToken", Some(String(Secret.reveal grant.AccessToken))
                  "accessTokenExpiresAt", Some(String(formatInstant grant.AccessTokenExpiresAt))
                  "refreshToken", Some(String(Secret.reveal grant.RefreshToken))
                  "refreshTokenExpiresAt", Some(String(formatInstant grant.RefreshTokenExpiresAt))
                  "identity",
                  identity
                  |> Option.map (fun i ->
                      let (ProviderId provider) = i.Provider

                      objectOf
                          [ "provider", Some(String provider)
                            "subject", Some(String i.Subject)
                            "login", Some(String i.Login)
                            "name", i.Name |> Option.map String ]) ]
        )

    let private identityOf root =
        JsonRead.child "identity" root
        |> Option.bind (fun i ->
            match text "provider" i, text "subject" i, text "login" i with
            | Some provider, Some subject, Some login ->
                Some
                    { Provider = ProviderId provider
                      Subject = subject
                      Login = login
                      Name = text "name" i }
            | _ -> None)

    /// Reads a token response. The identity is `None` when absent.
    let decodeGrant (body: string) : (TokenGrant * Identity option) option =
        body
        |> JsonRead.object (fun root ->
            match
                text "accessToken" root,
                text "accessTokenExpiresAt" root |> Option.bind parseInstant,
                text "refreshToken" root,
                text "refreshTokenExpiresAt" root |> Option.bind parseInstant
            with
            | Some access, Some accessExpiry, Some refresh, Some refreshExpiry ->
                Some(
                    { AccessToken = Secret.create access
                      AccessTokenExpiresAt = accessExpiry
                      RefreshToken = Secret.create refresh
                      RefreshTokenExpiresAt = refreshExpiry },
                    identityOf root
                )
            | _ -> None)

    let encodeRefusal (refusal: Refusal) = render (Object [ "error", String(code refusal) ])

    /// Reads a refusal body: `{"error": "<code>"}` with a known code.
    let decodeRefusal (body: string) =
        body |> JsonRead.object (text "error") |> Option.bind ofCode
