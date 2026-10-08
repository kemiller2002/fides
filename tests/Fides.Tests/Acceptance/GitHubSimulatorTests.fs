/// The simulator behaves as GitHub documents. If it did not, scenarios that
/// pass against it would prove nothing about GitHub.
module Fides.Tests.Acceptance.GitHubSimulatorTests

open System
open System.Text
open Xunit
open Fides.Tests.Acceptance.GitHubSimulator
open Fides.Tests.Acceptance.Scenarios

let private formBody (pairs: (string * string) list) =
    pairs |> List.map (fun (k, v) -> Uri.EscapeDataString k + "=" + Uri.EscapeDataString v) |> String.concat "&"

let private tokenRequest (pairs: (string * string) list) =
    { Method = "POST"
      Url = "https://github.com/login/oauth/access_token"
      Headers = [ "Accept", "application/json"; "Content-Type", "application/x-www-form-urlencoded" ]
      Body = formBody ([ "client_id", fixtureClientId; "client_secret", fixtureClientSecret ] @ pairs) }

let private codeRequest code codeVerifier redirectUri =
    tokenRequest [ "code", code; "code_verifier", codeVerifier; "redirect_uri", redirectUri ]

let private get url token =
    { Method = "GET"; Url = url; Headers = [ "Authorization", "Bearer " + token ]; Body = "" }

let private body =
    function
    | Responded(_, body) -> body
    | Unreachable -> failwith "unreachable"

let private status =
    function
    | Responded(status, _) -> status
    | Unreachable -> -1

let private redirect = chrona.RedirectUris.Head

let private signedIn () =
    initial epoch |> authorize octocat redirect (challengeOf verifier)

[<Fact>]
let ``S256 challenge matches RFC 7636 appendix B`` () =
    Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challengeOf verifier)

[<Fact>]
let ``a code exchanges once for an 8-hour token and a 6-month refresh token`` () =
    let world, code = signedIn ()
    let world, first = handle world (codeRequest code verifier redirect)
    Assert.Equal(200, status first)
    Assert.Contains("\"expires_in\":28800", body first)
    Assert.Contains("\"refresh_token_expires_in\":15811200", body first)
    let _, second = handle world (codeRequest code verifier redirect)
    Assert.Equal(Some "bad_verification_code", field "error" (body second))

[<Fact>]
let ``a code is bound to the PKCE challenge`` () =
    let world, code = signedIn ()
    let _, outcome = handle world (codeRequest code otherVerifier redirect)
    Assert.Equal(Some "bad_verification_code", field "error" (body outcome))

[<Fact>]
let ``a code is bound to its redirect URI`` () =
    let world, code = signedIn ()
    let _, outcome = handle world (codeRequest code verifier "https://attacker.example/cb")
    Assert.Equal(Some "redirect_uri_mismatch", field "error" (body outcome))

[<Fact>]
let ``a code expires after ten minutes`` () =
    let world, code = signedIn ()
    let _, outcome = handle (advance (TimeSpan.FromMinutes 11.) world) (codeRequest code verifier redirect)
    Assert.Equal(Some "bad_verification_code", field "error" (body outcome))

[<Fact>]
let ``wrong client credentials are refused`` () =
    let world, code = signedIn ()

    let request =
        { codeRequest code verifier redirect with
            Body = (codeRequest code verifier redirect).Body.Replace(fixtureClientSecret, "wrong") }

    let _, outcome = handle world request
    Assert.Equal(Some "incorrect_client_credentials", field "error" (body outcome))

[<Fact>]
let ``a refresh rotates the pair and retires the old refresh token`` () =
    let world, code = signedIn ()
    let world, issued = handle world (codeRequest code verifier redirect)
    let refresh = (field "refresh_token" (body issued)).Value
    let world, rotated = handle world (tokenRequest [ "grant_type", "refresh_token"; "refresh_token", refresh ])
    Assert.NotEqual(field "access_token" (body issued), field "access_token" (body rotated))
    let _, again = handle world (tokenRequest [ "grant_type", "refresh_token"; "refresh_token", refresh ])
    Assert.Equal(Some "bad_refresh_token", field "error" (body again))

[<Fact>]
let ``a refresh token expires`` () =
    let world, code = signedIn ()
    let world, issued = handle world (codeRequest code verifier redirect)
    let refresh = (field "refresh_token" (body issued)).Value
    let late = advance (refreshLifetime + TimeSpan.FromSeconds 1.) world
    let _, outcome = handle late (tokenRequest [ "grant_type", "refresh_token"; "refresh_token", refresh ])
    Assert.Equal(Some "bad_refresh_token", field "error" (body outcome))

[<Fact>]
let ``the user endpoint reports the identity for a valid token and 401 once revoked`` () =
    let world, code = signedIn ()
    let world, issued = handle world (codeRequest code verifier redirect)
    let access = (field "access_token" (body issued)).Value
    let world, user = handle world (get "https://api.github.com/user" access)
    Assert.Equal(Some "octocat", field "login" (body user))

    let revoke =
        { Method = "DELETE"
          Url = $"https://api.github.com/applications/{fixtureClientId}/token"
          Headers = [ "Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(fixtureClientId + ":" + fixtureClientSecret)) ]
          Body = $"{{\"access_token\":\"{access}\"}}" }

    let world, revoked = handle world revoke
    Assert.Equal(204, status revoked)
    let _, after = handle world (get "https://api.github.com/user" access)
    Assert.Equal(401, status after)

[<Fact>]
let ``an access token expires after eight hours`` () =
    let world, code = signedIn ()
    let world, issued = handle world (codeRequest code verifier redirect)
    let access = (field "access_token" (body issued)).Value
    let _, after = handle (advance (accessLifetime + TimeSpan.FromSeconds 1.) world) (get "https://api.github.com/user" access)
    Assert.Equal(401, status after)

[<Fact>]
let ``a repository the token cannot see is not found`` () =
    let world, code = signedIn ()
    let world, issued = handle world (codeRequest code verifier redirect)
    let access = (field "access_token" (body issued)).Value
    let _, denied = handle world (get "https://api.github.com/repos/echelon-data/summa" access)
    Assert.Equal(404, status denied)
    let _, granted = handle (grantRead "echelon-data/summa" octocat world) (get "https://api.github.com/repos/echelon-data/summa" access)
    Assert.Equal(200, status granted)

[<Fact>]
let ``outages fail the affected host`` () =
    let world, code = signedIn ()
    let _, down = handle { world with Outage = TokenEndpointDown } (codeRequest code verifier redirect)
    Assert.Equal(503, status down)
    let _, unreachable = handle { world with Outage = NetworkDown } (codeRequest code verifier redirect)
    Assert.Equal(Unreachable, unreachable)
    let _, tokenOk = handle { world with Outage = ApiDown } (codeRequest code verifier redirect)
    Assert.Equal(200, status tokenOk)

[<Fact>]
let ``a GitHub App without token expiry issues a token with no expiry or refresh token`` () =
    let world, code = signedIn ()
    let _, issued = handle { world with ExpiringTokens = false } (codeRequest code verifier redirect)
    Assert.True((field "access_token" (body issued)).IsSome)
    Assert.Equal(None, field "refresh_token" (body issued))
