/// A pure model of the parts of GitHub that Fides talks to, following
/// GitHub's documentation for GitHub App user-to-server tokens:
///
/// - `POST https://github.com/login/oauth/access_token` exchanges a code
///   (single use, 10 minutes, bound to the redirect URI and, with PKCE, to the
///   S256 challenge) or rotates a refresh token. Failures are HTTP 200 with an
///   `error` field (`bad_verification_code`, `bad_refresh_token`,
///   `incorrect_client_credentials`, `redirect_uri_mismatch`).
/// - Access tokens expire after 8 hours (`expires_in: 28800`) and refresh
///   tokens after about 6 months (`refresh_token_expires_in: 15811200`); every
///   refresh issues a new pair and retires the old refresh token.
/// - `GET https://api.github.com/user` answers 401 for an invalid token.
/// - `GET https://api.github.com/repos/{owner}/{repo}` answers 404 when the
///   token cannot see the repository.
/// - `DELETE https://api.github.com/applications/{client_id}/token` (Basic
///   client_id:client_secret, body `{"access_token": ...}`) revokes the grant.
///
/// The simulator is a state transition: `handle world request` returns the new
/// world and the outcome. Every token it issues is visibly synthetic.
module Fides.Acceptance.GitHubSimulator

open System
open System.Security.Cryptography
open System.Text
open System.Text.Json

/// A request as the exchange sends it to the provider.
type SimRequest =
    { Method: string
      Url: string
      Headers: (string * string) list
      Body: string }

/// What the provider did with a request.
type SimOutcome =
    | Responded of status: int * body: string
    | Unreachable

type User = { Id: int64; Login: string; Name: string }

type CodeGrant =
    { User: int64
      ClientId: string
      RedirectUri: string
      Challenge: string
      IssuedAt: DateTimeOffset
      Used: bool }

type AccessRecord =
    { User: int64
      ExpiresAt: DateTimeOffset
      Revoked: bool
      Grant: int }

type RefreshRecord =
    { User: int64
      ExpiresAt: DateTimeOffset
      Retired: bool
      Grant: int }

type Outage =
    | NoOutage
    | TokenEndpointDown
    | ApiDown
    | NetworkDown

type World =
    { Now: DateTimeOffset
      ClientId: string
      ClientSecret: string
      /// False models a GitHub App registered without "expire user
      /// authorization tokens": the token response has no expiry or refresh token.
      ExpiringTokens: bool
      Users: Map<int64, User>
      Codes: Map<string, CodeGrant>
      AccessTokens: Map<string, AccessRecord>
      RefreshTokens: Map<string, RefreshRecord>
      /// (owner/repo, user id) pairs the user's token can read.
      Readable: Set<string * int64>
      Outage: Outage
      /// True models the person revoking the App's authorization the moment
      /// after a token is issued, before Fides resolves the identity.
      RevokeOnIssue: bool
      Issued: int
      Requests: SimRequest list }

let accessLifetime = TimeSpan.FromSeconds 28800.
let refreshLifetime = TimeSpan.FromSeconds 15811200.
let codeLifetime = TimeSpan.FromMinutes 10.

let octocat = { Id = 583231L; Login = "octocat"; Name = "The Octocat" }
let hubot = { Id = 2L; Login = "hubot"; Name = "Hubot" }

/// The client secret the fixtures use. It is visibly not a real secret.
let fixtureClientSecret = "fixture-client-secret-not-real-0000"
let fixtureClientId = "Iv23fixtureclientid"

let initial (now: DateTimeOffset) =
    { Now = now
      ClientId = fixtureClientId
      ClientSecret = fixtureClientSecret
      ExpiringTokens = true
      Users = Map.ofList [ octocat.Id, octocat; hubot.Id, hubot ]
      Codes = Map.empty
      AccessTokens = Map.empty
      RefreshTokens = Map.empty
      Readable = Set.empty
      Outage = NoOutage
      RevokeOnIssue = false
      Issued = 0
      Requests = [] }

/// BASE64URL(SHA256(verifier)), RFC 7636 S256.
let challengeOf (verifier: string) =
    SHA256.HashData(Encoding.ASCII.GetBytes verifier)
    |> Convert.ToBase64String
    |> fun s -> s.TrimEnd('=').Replace('+', '-').Replace('/', '_')

/// The person signs in at GitHub's authorize page and GitHub redirects back
/// with a fresh code bound to the redirect URI and the PKCE challenge.
let authorize (user: User) (redirectUri: string) (challenge: string) (world: World) =
    let code = sprintf "sim-code-%04d" (world.Issued + 1)

    let grant =
        { User = user.Id
          ClientId = world.ClientId
          RedirectUri = redirectUri
          Challenge = challenge
          IssuedAt = world.Now
          Used = false }

    { world with
        Issued = world.Issued + 1
        Codes = world.Codes.Add(code, grant) },
    code

let advance (by: TimeSpan) (world: World) = { world with Now = world.Now + by }

let grantRead (repository: string) (user: User) (world: World) =
    { world with Readable = world.Readable.Add(repository, user.Id) }

/// A JSON value, enough for GitHub's responses.
type Json =
    | S of string
    | N of int64
    | B of bool
    | O of (string * Json) list

let rec private toValue =
    function
    | S value -> Fides.JsonWrite.String value
    | N value -> Fides.JsonWrite.Number value
    | B value -> Fides.JsonWrite.Bool value
    | O pairs -> Fides.JsonWrite.Object(pairs |> List.map (fun (k, v) -> k, toValue v))

/// Reflection-free, so the simulator also runs under trimmed WebAssembly.
let render json = Fides.JsonWrite.render (toValue json)

let private json pairs = render (O pairs)

let private ok body = Responded(200, body)
let private oauthError code = ok (json [ "error", S code; "error_description", S "simulated" ])

let private form (body: string) =
    body.Split('&', StringSplitOptions.RemoveEmptyEntries)
    |> Array.choose (fun pair ->
        match pair.Split('=', 2) with
        | [| k; v |] -> Some(Uri.UnescapeDataString k, Uri.UnescapeDataString(v.Replace('+', ' ')))
        | _ -> None)
    |> Map.ofArray

let private header (name: string) (request: SimRequest) =
    request.Headers
    |> List.tryFind (fun (k, _) -> String.Equals(k, name, StringComparison.OrdinalIgnoreCase))
    |> Option.map snd

let private bearer request =
    header "Authorization" request
    |> Option.bind (fun value -> if value.StartsWith "Bearer " then Some(value.Substring 7) else None)

let private issue (user: int64) (grant: int option) (world: World) =
    let n = world.Issued + 1
    let access = sprintf "ghu_sim_%04d" n
    let refresh = sprintf "ghr_sim_%04d" n
    let grantId = defaultArg grant n

    let world =
        { world with
            Issued = n
            AccessTokens =
                world.AccessTokens.Add(
                    access,
                    { User = user
                      ExpiresAt = world.Now + accessLifetime
                      Revoked = world.RevokeOnIssue
                      Grant = grantId }
                )
            RefreshTokens =
                world.RefreshTokens.Add(
                    refresh,
                    { User = user
                      ExpiresAt = world.Now + refreshLifetime
                      Retired = false
                      Grant = grantId }
                ) }

    let body =
        if world.ExpiringTokens then
            json
                [ "access_token", S access
                  "expires_in", N 28800L
                  "refresh_token", S refresh
                  "refresh_token_expires_in", N 15811200L
                  "scope", S ""
                  "token_type", S "bearer" ]
        else
            json [ "access_token", S access; "scope", S ""; "token_type", S "bearer" ]

    world, ok body

let private exchangeCode (parameters: Map<string, string>) (world: World) =
    let get key = parameters.TryFind key |> Option.defaultValue ""

    match world.Codes.TryFind(get "code") with
    | Some grant when
        not grant.Used
        && world.Now - grant.IssuedAt <= codeLifetime
        && grant.RedirectUri = get "redirect_uri"
        && grant.Challenge = challengeOf (get "code_verifier")
        ->
        let world = { world with Codes = world.Codes.Add(get "code", { grant with Used = true }) }
        issue grant.User None world
    | Some grant when not grant.Used && grant.RedirectUri <> get "redirect_uri" -> world, oauthError "redirect_uri_mismatch"
    | _ -> world, oauthError "bad_verification_code"

let private refresh (parameters: Map<string, string>) (world: World) =
    let token = parameters.TryFind "refresh_token" |> Option.defaultValue ""

    match world.RefreshTokens.TryFind token with
    | Some record when not record.Retired && world.Now < record.ExpiresAt ->
        let world =
            { world with RefreshTokens = world.RefreshTokens.Add(token, { record with Retired = true }) }

        issue record.User (Some record.Grant) world
    | _ -> world, oauthError "bad_refresh_token"

let private validAccess (world: World) (request: SimRequest) =
    bearer request
    |> Option.bind world.AccessTokens.TryFind
    |> Option.filter (fun record -> not record.Revoked && world.Now < record.ExpiresAt)

let private badCredentials = Responded(401, json [ "message", S "Bad credentials" ])

let private revokeGrant (grant: int) (world: World) =
    { world with
        AccessTokens = world.AccessTokens |> Map.map (fun _ r -> if r.Grant = grant then { r with Revoked = true } else r)
        RefreshTokens = world.RefreshTokens |> Map.map (fun _ r -> if r.Grant = grant then { r with Retired = true } else r) }

let private basicCredentials (world: World) =
    "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(world.ClientId + ":" + world.ClientSecret))

let private route (world: World) (request: SimRequest) =
    let uri = Uri request.Url

    match request.Method, uri.Host, uri.AbsolutePath with
    | "POST", "github.com", "/login/oauth/access_token" ->
        let parameters = form request.Body

        if parameters.TryFind "client_id" <> Some world.ClientId
           || parameters.TryFind "client_secret" <> Some world.ClientSecret then
            world, oauthError "incorrect_client_credentials"
        elif parameters.TryFind "grant_type" = Some "refresh_token" then
            refresh parameters world
        else
            exchangeCode parameters world
    | "GET", "api.github.com", "/user" ->
        match validAccess world request with
        | Some record ->
            let user = world.Users[record.User]
            world, ok (json [ "id", N user.Id; "login", S user.Login; "name", S user.Name ])
        | None -> world, badCredentials
    | "GET", "api.github.com", path when path.StartsWith "/repos/" ->
        let repository = path.Substring "/repos/".Length

        match validAccess world request with
        | Some record when world.Readable.Contains(repository, record.User) ->
            world, ok (json [ "full_name", S repository; "permissions", O [ "pull", B true; "push", B true; "admin", B false ] ])
        | Some _ -> world, Responded(404, json [ "message", S "Not Found" ])
        | None -> world, badCredentials
    | "DELETE", "api.github.com", path when path = $"/applications/{world.ClientId}/token" ->
        if header "Authorization" request <> Some(basicCredentials world) then
            world, Responded(401, json [ "message", S "Requires authentication" ])
        else
            let token =
                try
                    use document = JsonDocument.Parse request.Body
                    document.RootElement.GetProperty("access_token").GetString() |> Option.ofObj
                with _ ->
                    None

            match token |> Option.bind world.AccessTokens.TryFind with
            | Some record when not record.Revoked -> revokeGrant record.Grant world, Responded(204, "")
            | _ -> world, Responded(404, json [ "message", S "Not Found" ])
    | _ -> world, Responded(404, json [ "message", S "Not Found" ])

/// GitHub's answer to one request, and the world afterwards. Every request is
/// recorded, including those that an outage swallows.
let handle (world: World) (request: SimRequest) =
    let world = { world with Requests = world.Requests @ [ request ] }
    let host = (Uri request.Url).Host

    match world.Outage, host with
    | NetworkDown, _ -> world, Unreachable
    | TokenEndpointDown, "github.com"
    | ApiDown, "api.github.com" -> world, Responded(503, "<html>unicorn</html>")
    | _ -> route world request

/// Whether an access token can still be used.
let accessTokenValid (token: string) (world: World) =
    world.AccessTokens.TryFind token
    |> Option.exists (fun r -> not r.Revoked && world.Now < r.ExpiresAt)
