/// Fides's acceptance scenarios (FID-TEST-001, issue #1), written before the
/// production code and against the public contract in
/// docs/architecture/EXCHANGE-PROTOCOL.md: HTTP paths, JSON bodies, status
/// codes, refusal codes, callback outcomes, session states and token-provider
/// results. They name no internal type, so the implementation can change
/// without the scenarios moving.
///
/// Exchange scenarios run against the exchange core with the GitHub simulator
/// standing in for GitHub (WI-0007) and again through every hosting adapter
/// (WI-0010). Callback and session scenarios run against the client (WI-0009).
module Fides.Acceptance.Scenarios

open System
open System.Text.Json
open Fides.Acceptance.GitHubSimulator

/// The issue #1 categories, plus the supporting paths the protocol defines.
type Category =
    | InvalidState
    | Replay
    | WrongCallback
    | RevokedIdentity
    | ExpiredSession
    | RepositoryAccessDenied
    | ProviderOutage
    | Supporting

/// The categories issue #1 requires acceptance tests for.
let requiredCategories =
    [ InvalidState; Replay; WrongCallback; RevokedIdentity; ExpiredSession; RepositoryAccessDenied; ProviderOutage ]

/// An application as registered with the exchange.
type Registration =
    { Application: string
      Origins: string list
      RedirectUris: string list
      RequiredRepository: string option }

let chrona =
    { Application = "chrona-test"
      Origins = [ "https://chrona.example" ]
      RedirectUris = [ "https://chrona.example/auth/callback" ]
      RequiredRepository = None }

let summa =
    { Application = "summa-test"
      Origins = [ "https://summa.example" ]
      RedirectUris = [ "https://summa.example/auth/callback" ]
      RequiredRepository = Some "echelon-data/summa" }

let registrations = [ chrona; summa ]

/// The RFC 7636 appendix B verifier: a well-formed PKCE verifier.
let verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"

/// A different well-formed verifier.
let otherVerifier = "Zm9vYmFyYmF6cXV4cXV1eGNvcmdlZ3JhdWx0Z2FycGx5"

/// The fixed instant every scenario starts at.
let epoch = DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero)

/// One HTTP request to the exchange.
type Call =
    { Method: string
      Path: string
      Origin: string option
      Body: string }

/// The expected answer to a call.
type Expect =
    | Status of int
    | Refused of status: int * code: string

/// What earlier steps produced: the code the person brought back, and the
/// bodies of earlier exchange responses.
type Context = { Code: string; Responses: string list }

type Step =
    { Call: Context -> Call
      Expect: Expect
      /// Simulated time that passes before this step.
      AdvanceBefore: TimeSpan }

type ExchangeScenario =
    { Id: string
      Category: Category
      Requirements: string list
      Description: string
      Arrange: World -> World * Context
      /// Whether the exchange can read the client secret.
      SecretAvailable: bool
      Steps: Step list
      /// False: the exchange must refuse before any provider request.
      ProviderCalled: bool
      /// A check on the simulated GitHub afterwards; `None` when it holds.
      Afterwards: World -> Context -> string option }

/// A string field of a JSON body, if present.
let field (name: string) (body: string) =
    try
        use document = JsonDocument.Parse body

        match document.RootElement.TryGetProperty name with
        | true, value when value.ValueKind = JsonValueKind.String -> value.GetString() |> Option.ofObj
        | _ -> None
    with :? JsonException ->
        None

let private serialize (pairs: (string * string) list) = JsonSerializer.Serialize(dict pairs)

let tokenBody (registration: Registration) (code: string) (codeVerifier: string) (redirectUri: string) =
    serialize [ "application", registration.Application; "code", code; "codeVerifier", codeVerifier; "redirectUri", redirectUri ]

let private post path origin body =
    { Method = "POST"; Path = path; Origin = origin; Body = body }

let private token (registration: Registration) (context: Context) =
    post "/v1/token" (Some registration.Origins.Head) (tokenBody registration context.Code verifier registration.RedirectUris.Head)

let private refreshWith (registration: Registration) (refreshToken: string) =
    post "/v1/refresh" (Some registration.Origins.Head) (serialize [ "application", registration.Application; "refreshToken", refreshToken ])

let private previous (name: string) (index: int) (context: Context) =
    context.Responses |> List.item index |> field name |> Option.defaultValue "missing"

/// The person signed in at GitHub and came back to the application's callback.
let signedInAtGitHub (registration: Registration) (user: User) (world: World) =
    let world, code = authorize user registration.RedirectUris.Head (challengeOf verifier) world
    world, { Code = code; Responses = [] }

let private arrange = signedInAtGitHub chrona octocat
let private holds _ _ = None
let private step call expect =
    { Call = call; Expect = expect; AdvanceBefore = TimeSpan.Zero }

let private scenario id category requirements description steps =
    { Id = id
      Category = category
      Requirements = requirements
      Description = description
      Arrange = arrange
      SecretAvailable = true
      Steps = steps
      ProviderCalled = true
      Afterwards = holds }

let private refusedBeforeProvider id category requirements description call (status, code) =
    { scenario id category requirements description [ step call (Refused(status, code)) ] with
        ProviderCalled = false }

let exchangeScenarios: ExchangeScenario list =
    [ scenario "sign-in-succeeds" Supporting [ "FID-EXC-001"; "FID-PRV-003" ] "A valid code, verifier and redirect URI sign the person in with GitHub's identity" [
          step (token chrona) (Status 200)
      ]
      |> fun s ->
          { s with
              Afterwards =
                  fun world _ ->
                      match world.AccessTokens |> Map.toList with
                      | [ (_, record) ] when record.User = octocat.Id -> None
                      | tokens -> Some $"expected one token for octocat, found {tokens.Length}" }

      refusedBeforeProvider "wrong-callback" WrongCallback [ "FID-TB-003"; "FID-EXC-001" ] "A redirect URI the application did not register is refused before GitHub is called"
          (fun c -> post "/v1/token" (Some "https://chrona.example") (tokenBody chrona c.Code verifier "https://attacker.example/auth/callback"))
          (400, "redirect_uri_not_allowed")

      refusedBeforeProvider "wrong-callback-trailing-slash" WrongCallback [ "FID-TB-003" ] "Redirect URIs match exactly: a trailing slash is a different URI"
          (fun c -> post "/v1/token" (Some "https://chrona.example") (tokenBody chrona c.Code verifier "https://chrona.example/auth/callback/"))
          (400, "redirect_uri_not_allowed")

      refusedBeforeProvider "wrong-callback-case" WrongCallback [ "FID-TB-003" ] "Redirect URIs match exactly: case is significant"
          (fun c -> post "/v1/token" (Some "https://chrona.example") (tokenBody chrona c.Code verifier "https://Chrona.example/auth/callback"))
          (400, "redirect_uri_not_allowed")

      refusedBeforeProvider "wrong-callback-query" WrongCallback [ "FID-TB-003" ] "Redirect URIs match exactly: an added query is a different URI"
          (fun c -> post "/v1/token" (Some "https://chrona.example") (tokenBody chrona c.Code verifier "https://chrona.example/auth/callback?next=https://attacker.example"))
          (400, "redirect_uri_not_allowed")

      refusedBeforeProvider "wrong-callback-other-application" WrongCallback [ "FID-TB-003" ] "Another application's registered redirect URI is not this application's"
          (fun c -> post "/v1/token" (Some "https://chrona.example") (tokenBody chrona c.Code verifier "https://summa.example/auth/callback"))
          (400, "redirect_uri_not_allowed")

      refusedBeforeProvider "cross-origin" WrongCallback [ "FID-HOST-005" ] "A request from an origin the application did not register is refused"
          (fun c -> post "/v1/token" (Some "https://attacker.example") (tokenBody chrona c.Code verifier chrona.RedirectUris.Head))
          (403, "origin_not_allowed")

      refusedBeforeProvider "missing-origin" WrongCallback [ "FID-HOST-005" ] "A request without an Origin header is refused"
          (fun c -> post "/v1/token" None (tokenBody chrona c.Code verifier chrona.RedirectUris.Head))
          (403, "origin_not_allowed")

      refusedBeforeProvider "unknown-application" Supporting [ "FID-EXC-001" ] "An application that is not registered is refused"
          (fun c -> post "/v1/token" (Some "https://chrona.example") (tokenBody { chrona with Application = "unregistered" } c.Code verifier chrona.RedirectUris.Head))
          (400, "unknown_application")

      refusedBeforeProvider "malformed-verifier" InvalidState [ "FID-EXC-001" ] "A PKCE verifier that is not 43-128 unreserved characters is refused"
          (fun c -> post "/v1/token" (Some "https://chrona.example") (tokenBody chrona c.Code "too-short" chrona.RedirectUris.Head))
          (400, "invalid_code_verifier")

      refusedBeforeProvider "malformed-request" Supporting [ "FID-EXC-001" ] "A body that is not the protocol's JSON is refused"
          (fun _ -> post "/v1/token" (Some "https://chrona.example") "{not json")
          (400, "malformed_request")

      refusedBeforeProvider "oversized-request" Supporting [ "FID-EXC-001" ] "A body over 8 KiB is refused"
          (fun c -> post "/v1/token" (Some "https://chrona.example") (tokenBody chrona (c.Code + String('x', 9000)) verifier chrona.RedirectUris.Head))
          (400, "malformed_request")

      refusedBeforeProvider "unknown-path" Supporting [ "FID-HOST-001" ] "An unknown path is not found"
          (fun _ -> post "/v1/session" (Some "https://chrona.example") "{}")
          (404, "not_found")

      refusedBeforeProvider "wrong-method" Supporting [ "FID-HOST-001" ] "A known path with the wrong method is refused"
          (fun _ -> { Method = "GET"; Path = "/v1/token"; Origin = Some "https://chrona.example"; Body = "" })
          (405, "method_not_allowed")

      { refusedBeforeProvider "secret-unavailable" Supporting [ "FID-EXC-002"; "FID-HOST-002" ] "When the client secret cannot be read, nothing is sent to GitHub"
            (token chrona) (500, "configuration_unavailable") with
          SecretAvailable = false }

      scenario "wrong-verifier" InvalidState [ "FID-EXC-001" ] "A well-formed verifier that does not match the code's challenge is refused by GitHub" [
          step (fun c -> post "/v1/token" (Some "https://chrona.example") (tokenBody chrona c.Code otherVerifier chrona.RedirectUris.Head)) (Refused(400, "code_rejected"))
      ]

      scenario "replayed-code" Replay [ "FID-EXC-003" ] "A code that was already exchanged is refused the second time" [
          step (token chrona) (Status 200)
          step (token chrona) (Refused(400, "code_rejected"))
      ]

      { scenario "expired-code" Replay [ "FID-EXC-003" ] "A code older than GitHub's ten minutes is refused" [
            step (token chrona) (Refused(400, "code_rejected"))
        ] with
          Arrange = fun world -> arrange world |> fun (w, c) -> advance (TimeSpan.FromMinutes 11.) w, c }

      scenario "refresh-succeeds" Supporting [ "FID-EXC-001"; "FID-PRV-002" ] "A refresh token is exchanged for a new pair through the exchange" [
          step (token chrona) (Status 200)
          step (fun c -> refreshWith chrona (previous "refreshToken" 0 c)) (Status 200)
      ]

      scenario "replayed-refresh-token" Replay [ "FID-EXC-003" ] "A refresh token is single use: GitHub retires it on refresh" [
          step (token chrona) (Status 200)
          step (fun c -> refreshWith chrona (previous "refreshToken" 0 c)) (Status 200)
          step (fun c -> refreshWith chrona (previous "refreshToken" 0 c)) (Refused(401, "refresh_rejected"))
      ]

      scenario "revoked-identity" RevokedIdentity [ "FID-TB-005" ] "After the grant is revoked, refreshing is refused" [
          step (token chrona) (Status 200)
          step (fun c -> post "/v1/revoke" (Some "https://chrona.example") (serialize [ "application", "chrona-test"; "accessToken", previous "accessToken" 0 c ])) (Status 204)
          step (fun c -> refreshWith chrona (previous "refreshToken" 0 c)) (Refused(401, "refresh_rejected"))
      ]

      { scenario "identity-revoked-at-sign-in" RevokedIdentity [ "FID-PRV-003"; "FID-TB-005" ] "A token GitHub refuses when resolving the identity signs no one in" [
            step (token chrona) (Refused(401, "identity_revoked"))
        ] with
          Arrange = fun world -> arrange { world with RevokeOnIssue = true } }

      { scenario "revoke-succeeds" Supporting [ "FID-TB-005" ] "Sign-out revokes the token at GitHub" [
            step (token chrona) (Status 200)
            step (fun c -> post "/v1/revoke" (Some "https://chrona.example") (serialize [ "application", "chrona-test"; "accessToken", previous "accessToken" 0 c ])) (Status 204)
        ] with
          Afterwards =
              fun world _ ->
                  if world.AccessTokens |> Map.forall (fun _ r -> r.Revoked) then None
                  else Some "a token is still valid after revocation" }

      scenario "expired-session" ExpiredSession [ "FID-TB-005" ] "A refresh token past its six-month expiry is refused" [
          step (token chrona) (Status 200)
          { step (fun c -> refreshWith chrona (previous "refreshToken" 0 c)) (Refused(401, "refresh_rejected")) with
              AdvanceBefore = refreshLifetime + TimeSpan.FromMinutes 1. }
      ]

      { scenario "repository-access-denied" RepositoryAccessDenied [ "FID-TB-004" ] "An application with a required repository refuses a person whose token cannot read it, and the token is revoked" [
            step (fun c -> post "/v1/token" (Some "https://summa.example") (tokenBody summa c.Code verifier summa.RedirectUris.Head)) (Refused(403, "repository_access_denied"))
        ] with
          Arrange = signedInAtGitHub summa hubot
          Afterwards =
              fun world _ ->
                  if world.AccessTokens |> Map.forall (fun _ r -> r.Revoked) then None
                  else Some "the unusable token was not revoked" }

      { scenario "repository-access-granted" RepositoryAccessDenied [ "FID-TB-004" ] "A person whose token can read the required repository signs in" [
            step (fun c -> post "/v1/token" (Some "https://summa.example") (tokenBody summa c.Code verifier summa.RedirectUris.Head)) (Status 200)
        ] with
          Arrange = fun world -> world |> grantRead "echelon-data/summa" octocat |> signedInAtGitHub summa octocat }

      { scenario "provider-outage" ProviderOutage [ "FID-EXC-004" ] "GitHub's token endpoint failing is a typed outage and issues nothing" [
            step (token chrona) (Refused(503, "provider_unavailable"))
        ] with
          Arrange = fun world -> arrange world |> fun (w, c) -> { w with Outage = TokenEndpointDown }, c }

      { scenario "provider-unreachable" ProviderOutage [ "FID-EXC-004" ] "GitHub being unreachable is a typed outage" [
            step (token chrona) (Refused(503, "provider_unavailable"))
        ] with
          Arrange = fun world -> arrange world |> fun (w, c) -> { w with Outage = NetworkDown }, c }

      { scenario "identity-outage" ProviderOutage [ "FID-EXC-004" ] "GitHub's API failing after the token is issued returns no partial session" [
            step (token chrona) (Refused(503, "provider_unavailable"))
        ] with
          Arrange = fun world -> arrange world |> fun (w, c) -> { w with Outage = ApiDown }, c }

      { scenario "non-expiring-token" Supporting [ "FID-PRV-002" ] "A GitHub App without token expiry is a provider contract violation, not a non-expiring session" [
            step (token chrona) (Refused(502, "provider_contract_violation"))
        ] with
          Arrange = fun world -> arrange { world with ExpiringTokens = false } } ]

// ---------------------------------------------------------------------------
// Client callback scenarios (run against the client in WI-0009).

/// A pending sign-in this tab stored before navigating to GitHub.
type Pending = { State: string; AgeMinutes: int }

type CallbackStep =
    { Query: (string * string) list
      /// The client's outcome code (EXCHANGE-PROTOCOL.md section 5).
      Expect: string
      ExchangeCalled: bool }

type CallbackScenario =
    { Id: string
      Category: Category
      Requirements: string list
      Description: string
      Pending: Pending option
      /// What the exchange answers if it is called: `signed_in` or a refusal code.
      ExchangeAnswer: string
      Steps: CallbackStep list }

let private pendingState = "c3RhdGUtZml4dHVyZS0wMDAwMDAwMDAwMDAwMDAwMDA"

let private callback query expect called =
    { Query = query; Expect = expect; ExchangeCalled = called }

let callbackScenarios: CallbackScenario list =
    [ { Id = "callback-succeeds"; Category = Supporting; Requirements = [ "FID-CLI-001" ]
        Description = "A callback with the pending state completes sign-in"
        Pending = Some { State = pendingState; AgeMinutes = 1 }; ExchangeAnswer = "signed_in"
        Steps = [ callback [ "code", "sim-code-0001"; "state", pendingState ] "signed_in" true ] }
      { Id = "invalid-state"; Category = InvalidState; Requirements = [ "FID-EXC-001" ]
        Description = "A callback whose state differs from the pending one is refused without calling the exchange"
        Pending = Some { State = pendingState; AgeMinutes = 1 }; ExchangeAnswer = "signed_in"
        Steps = [ callback [ "code", "sim-code-0001"; "state", "Zm9yZ2VkLXN0YXRlLTAwMDAwMDAwMDAwMDAwMDAwMDA" ] "state_invalid" false ] }
      { Id = "missing-state"; Category = InvalidState; Requirements = [ "FID-EXC-001" ]
        Description = "A callback without state is refused"
        Pending = Some { State = pendingState; AgeMinutes = 1 }; ExchangeAnswer = "signed_in"
        Steps = [ callback [ "code", "sim-code-0001" ] "state_invalid" false ] }
      { Id = "unsolicited-callback"; Category = InvalidState; Requirements = [ "FID-EXC-001" ]
        Description = "A callback this tab never started (login CSRF) is refused"
        Pending = None; ExchangeAnswer = "signed_in"
        Steps = [ callback [ "code", "sim-code-0001"; "state", pendingState ] "state_invalid" false ] }
      { Id = "replayed-state"; Category = Replay; Requirements = [ "FID-EXC-003" ]
        Description = "The same callback a second time finds the pending sign-in consumed"
        Pending = Some { State = pendingState; AgeMinutes = 1 }; ExchangeAnswer = "signed_in"
        Steps =
          [ callback [ "code", "sim-code-0001"; "state", pendingState ] "signed_in" true
            callback [ "code", "sim-code-0001"; "state", pendingState ] "state_invalid" false ] }
      { Id = "expired-state"; Category = InvalidState; Requirements = [ "FID-EXC-001" ]
        Description = "A pending sign-in older than ten minutes is refused"
        Pending = Some { State = pendingState; AgeMinutes = 11 }; ExchangeAnswer = "signed_in"
        Steps = [ callback [ "code", "sim-code-0001"; "state", pendingState ] "state_expired" false ] }
      { Id = "user-denied"; Category = Supporting; Requirements = [ "FID-CLI-003" ]
        Description = "The person declining at GitHub is a typed outcome"
        Pending = Some { State = pendingState; AgeMinutes = 1 }; ExchangeAnswer = "signed_in"
        Steps = [ callback [ "error", "access_denied"; "state", pendingState ] "provider_denied" false ] }
      { Id = "callback-exchange-refused"; Category = WrongCallback; Requirements = [ "FID-CLI-003" ]
        Description = "An exchange refusal reaches the application as its code"
        Pending = Some { State = pendingState; AgeMinutes = 1 }; ExchangeAnswer = "redirect_uri_not_allowed"
        Steps = [ callback [ "code", "sim-code-0001"; "state", pendingState ] "redirect_uri_not_allowed" true ] }
      { Id = "callback-provider-outage"; Category = ProviderOutage; Requirements = [ "FID-EXC-004"; "FID-CLI-003" ]
        Description = "An outage during the exchange is a typed outcome with no session"
        Pending = Some { State = pendingState; AgeMinutes = 1 }; ExchangeAnswer = "provider_unavailable"
        Steps = [ callback [ "code", "sim-code-0001"; "state", pendingState ] "provider_unavailable" true ] } ]

// ---------------------------------------------------------------------------
// Session and token-provider scenarios (run against the client in WI-0009).

type SessionFixture =
    | NoSession
    | Session of accessExpiresInMinutes: int * refreshExpiresInMinutes: int

/// What the exchange's refresh endpoint answers if it is called.
type RefreshAnswer =
    | RefreshSucceeds
    | RefreshRefused
    | RefreshUnavailable
    | RefreshNotExpected

type SessionEvent =
    | ApplicationReportedUnauthorized
    | SignOut

type SessionScenario =
    { Id: string
      Category: Category
      Requirements: string list
      Description: string
      Session: SessionFixture
      Events: SessionEvent list
      Refresh: RefreshAnswer
      /// `token`, `none`, `expired` or `revoked` (EXCHANGE-PROTOCOL.md section 6).
      ExpectToken: string
      ExpectState: string }

let sessionScenarios: SessionScenario list =
    [ { Id = "token-current"; Category = Supporting; Requirements = [ "FID-CLI-001" ]
        Description = "A valid access token is handed to Arca without refreshing"
        Session = Session(120, 100000); Events = []; Refresh = RefreshNotExpected; ExpectToken = "token"; ExpectState = "signed_in" }
      { Id = "token-near-expiry-refreshes"; Category = ExpiredSession; Requirements = [ "FID-CLI-001" ]
        Description = "An access token inside the five-minute margin is refreshed first"
        Session = Session(3, 100000); Events = []; Refresh = RefreshSucceeds; ExpectToken = "token"; ExpectState = "signed_in" }
      { Id = "expired-session-refreshes"; Category = ExpiredSession; Requirements = [ "FID-CLI-003" ]
        Description = "An expired access token is refreshed"
        Session = Session(-30, 100000); Events = []; Refresh = RefreshSucceeds; ExpectToken = "token"; ExpectState = "signed_in" }
      { Id = "expired-refresh-token"; Category = ExpiredSession; Requirements = [ "FID-CLI-003" ]
        Description = "An expired access token whose refresh token has also expired yields expired, without calling the exchange"
        Session = Session(-30, -1); Events = []; Refresh = RefreshNotExpected; ExpectToken = "expired"; ExpectState = "expired" }
      { Id = "expired-session-outage"; Category = ProviderOutage; Requirements = [ "FID-EXC-004"; "FID-CLI-003" ]
        Description = "An expired access token that cannot be refreshed during an outage yields expired"
        Session = Session(-30, 100000); Events = []; Refresh = RefreshUnavailable; ExpectToken = "expired"; ExpectState = "provider_unavailable" }
      { Id = "revoked-refresh"; Category = RevokedIdentity; Requirements = [ "FID-TB-005"; "FID-CLI-003" ]
        Description = "A refresh refused before the refresh token's expiry means the grant was revoked"
        Session = Session(-30, 100000); Events = []; Refresh = RefreshRefused; ExpectToken = "revoked"; ExpectState = "revoked" }
      { Id = "revoked-identity-reported"; Category = RevokedIdentity; Requirements = [ "FID-TB-005"; "FID-CLI-003" ]
        Description = "An application reporting a 401 for the token revokes the session"
        Session = Session(120, 100000); Events = [ ApplicationReportedUnauthorized ]; Refresh = RefreshNotExpected; ExpectToken = "revoked"; ExpectState = "revoked" }
      { Id = "signed-out"; Category = Supporting; Requirements = [ "FID-CLI-003" ]
        Description = "With no session the token provider yields none"
        Session = NoSession; Events = []; Refresh = RefreshNotExpected; ExpectToken = "none"; ExpectState = "signed_out" }
      { Id = "sign-out-clears"; Category = Supporting; Requirements = [ "FID-TB-005"; "FID-CLI-002" ]
        Description = "Signing out clears the session; the token provider yields none"
        Session = Session(120, 100000); Events = [ SignOut ]; Refresh = RefreshNotExpected; ExpectToken = "none"; ExpectState = "signed_out" } ]
