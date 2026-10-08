namespace Fides

open System
open System.Text


/// GitHub as an identity provider, through a GitHub App's user-to-server
/// tokens with expiry and refresh (DF-FIDES-2026-0003).
module GitHub =
    /// Where GitHub lives. Defaults are github.com; GitHub Enterprise Server
    /// needs only different endpoints.
    type Endpoints =
        { AuthorizeUrl: string
          TokenUrl: string
          ApiUrl: string
          /// GitHub requires a User-Agent on API requests.
          UserAgent: string }

    let githubDotCom =
        { AuthorizeUrl = "https://github.com/login/oauth/authorize"
          TokenUrl = "https://github.com/login/oauth/access_token"
          ApiUrl = "https://api.github.com"
          UserAgent = "Fides" }

    let id = ProviderId "github"

    let private query (pairs: (string * string) list) =
        pairs |> List.map (fun (k, v) -> Uri.EscapeDataString k + "=" + Uri.EscapeDataString v) |> String.concat "&"

    let authorizationUrl (endpoints: Endpoints) (request: AuthorizationRequest) =
        endpoints.AuthorizeUrl
        + "?"
        + query
            [ "client_id", request.ClientId
              "redirect_uri", request.RedirectUri
              "state", request.State
              "code_challenge", request.CodeChallenge
              "code_challenge_method", "S256" ]

    let private tokenRequest (endpoints: Endpoints) (credentials: ClientCredentials) parameters =
        { Method = "POST"
          Url = endpoints.TokenUrl
          Headers = [ "Accept", "application/json"; "User-Agent", endpoints.UserAgent ]
          Body =
            Form(
                [ "client_id", credentials.ClientId
                  "client_secret", Secret.reveal credentials.ClientSecret ]
                @ parameters
            ) }

    let codeExchange endpoints credentials (code: AuthorizationCode) (verifier: CodeVerifier) (redirectUri: string) =
        tokenRequest endpoints credentials [ "code", Secret.reveal code; "code_verifier", Secret.reveal verifier; "redirect_uri", redirectUri ]

    let refresh endpoints credentials (refreshToken: RefreshToken) =
        tokenRequest endpoints credentials [ "grant_type", "refresh_token"; "refresh_token", Secret.reveal refreshToken ]

    let private apiHeaders (endpoints: Endpoints) =
        [ "Accept", "application/vnd.github+json"
          "X-GitHub-Api-Version", "2022-11-28"
          "User-Agent", endpoints.UserAgent ]

    let private bearer (token: AccessToken) = "Authorization", "Bearer " + Secret.reveal token

    let identityRequest (endpoints: Endpoints) token =
        { Method = "GET"
          Url = endpoints.ApiUrl + "/user"
          Headers = bearer token :: apiHeaders endpoints
          Body = NoBody }

    let revocation (endpoints: Endpoints) (credentials: ClientCredentials) (token: AccessToken) =
        let basic =
            Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials.ClientId + ":" + Secret.reveal credentials.ClientSecret))

        { Method = "DELETE"
          Url = endpoints.ApiUrl + "/applications/" + Uri.EscapeDataString credentials.ClientId + "/token"
          Headers = ("Authorization", "Basic " + basic) :: apiHeaders endpoints
          Body = Json(JsonWrite.render (JsonWrite.Object [ "access_token", JsonWrite.String(Secret.reveal token) ])) }

    let repositoryAccessRequest (endpoints: Endpoints) token (repository: RepositoryName) =
        { Method = "GET"
          Url = endpoints.ApiUrl + "/repos/" + RepositoryName.value repository
          Headers = bearer token :: apiHeaders endpoints
          Body = NoBody }

    /// 5xx, 429 and transport failures are outages; GitHub signals rate
    /// limits with 403 or 429.
    let private failure (status: int) = status >= 500 || status = 429

    /// Reads a token response. GitHub reports OAuth errors as HTTP 200 with an
    /// `error` field. A token without expiry and refresh token means the App is
    /// registered without "expire user authorization tokens", which Fides does
    /// not accept (DF-FIDES-2026-0003).
    let readTokenGrant (receivedAt: DateTimeOffset) (outcome: HttpOutcome) : Result<TokenGrant, ProviderRefusal> =
        match outcome with
        | Failed _ -> Error Unavailable
        | Responded response when failure response.Status -> Error Unavailable
        | Responded response when response.Status <> 200 -> Error(ContractViolation $"token endpoint answered {response.Status}")
        | Responded response ->
            let read root =
                match JsonRead.string "error" root with
                | Some "bad_verification_code"
                | Some "redirect_uri_mismatch" -> Some(Error CodeRejected)
                | Some "bad_refresh_token" -> Some(Error RefreshRejected)
                | Some "incorrect_client_credentials" -> Some(Error ClientCredentialsRejected)
                | Some _ -> Some(Error(ContractViolation "unrecognised OAuth error"))
                | None ->
                    match
                        JsonRead.string "access_token" root,
                        JsonRead.int64 "expires_in" root,
                        JsonRead.string "refresh_token" root,
                        JsonRead.int64 "refresh_token_expires_in" root
                    with
                    | Some access, Some expiresIn, Some refresh, Some refreshExpiresIn when
                        access.Length > 0 && refresh.Length > 0 && expiresIn > 0L && refreshExpiresIn > 0L
                        ->
                        match JsonRead.string "token_type" root with
                        | Some kind when not (String.Equals(kind, "bearer", StringComparison.OrdinalIgnoreCase)) ->
                            Some(Error(ContractViolation "token type is not bearer"))
                        | _ ->
                            Some(
                                Ok
                                    { AccessToken = Secret.create access
                                      AccessTokenExpiresAt = receivedAt.AddSeconds(float expiresIn)
                                      RefreshToken = Secret.create refresh
                                      RefreshTokenExpiresAt = receivedAt.AddSeconds(float refreshExpiresIn) }
                            )
                    | Some _, None, _, _
                    | Some _, _, None, _ -> Some(Error(ContractViolation "token without expiry or refresh token"))
                    | _ -> Some(Error(ContractViolation "token response is incomplete"))

            JsonRead.object read response.Body
            |> Option.defaultValue (Error(ContractViolation "token response is not a JSON object"))

    let readIdentity (outcome: HttpOutcome) : Result<Identity, ProviderRefusal> =
        match outcome with
        | Failed _ -> Error Unavailable
        | Responded { Status = 401 } -> Error TokenRejected
        | Responded response when failure response.Status || response.Status = 403 -> Error Unavailable
        | Responded response when response.Status <> 200 -> Error(ContractViolation $"user endpoint answered {response.Status}")
        | Responded response ->
            let read root =
                match JsonRead.int64 "id" root, JsonRead.string "login" root with
                | Some subject, Some login when login.Length > 0 ->
                    Some(
                        Ok
                            { Provider = id
                              Subject = string subject
                              Login = login
                              Name = JsonRead.string "name" root |> Option.filter (fun n -> n.Length > 0) }
                    )
                | _ -> None

            JsonRead.object read response.Body
            |> Option.defaultValue (Error(ContractViolation "user response lacks id or login"))

    /// 204 revokes. 404 and 422 mean the token is already unusable, which is
    /// what revocation wants, so they count as revoked.
    let readRevocation (outcome: HttpOutcome) : Result<unit, ProviderRefusal> =
        match outcome with
        | Failed _ -> Error Unavailable
        | Responded { Status = 204 }
        | Responded { Status = 404 }
        | Responded { Status = 422 } -> Ok()
        | Responded { Status = 401 } -> Error ClientCredentialsRejected
        | Responded response when failure response.Status -> Error Unavailable
        | Responded response -> Error(ContractViolation $"revocation answered {response.Status}")

    /// 200 is readable. GitHub hides repositories a token cannot see behind
    /// 404, and answers 403 when the App lacks the permission.
    let readRepositoryAccess (outcome: HttpOutcome) : Result<RepositoryAccessResult, ProviderRefusal> =
        match outcome with
        | Failed _ -> Error Unavailable
        | Responded { Status = 200 } -> Ok Readable
        | Responded { Status = 404 }
        | Responded { Status = 403 } -> Ok NotReadable
        | Responded { Status = 401 } -> Error TokenRejected
        | Responded response when failure response.Status -> Error Unavailable
        | Responded response -> Error(ContractViolation $"repository endpoint answered {response.Status}")

    /// GitHub through a GitHub App, at the given endpoints.
    let provider (endpoints: Endpoints) : IdentityProvider =
        { Id = id
          Capabilities = Set.ofList [ AuthorizationCodeWithPkce; ExpiringTokens; RefreshTokens; Revocation; RepositoryAccess ]
          Proves =
            [ "The person controlled the GitHub account at authorization time."
              "The person authorized this GitHub App."
              "The token reaches what the App's permissions, its installations and the person's own access allow."
              "The account's stable numeric id and current login, as GET /user reports them."
              "Whether a token or refresh grant is still valid, when asked." ]
          AuthorizationUrl = authorizationUrl endpoints
          CodeExchange = codeExchange endpoints
          Refresh = refresh endpoints
          ReadTokenGrant = readTokenGrant
          IdentityRequest = identityRequest endpoints
          ReadIdentity = readIdentity
          Revocation = revocation endpoints
          ReadRevocation = readRevocation
          RepositoryAccessRequest = repositoryAccessRequest endpoints
          ReadRepositoryAccess = readRepositoryAccess }
