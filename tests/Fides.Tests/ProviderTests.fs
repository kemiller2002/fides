module Fides.Tests.ProviderTests

open System
open Xunit
open Fides
open Fides.Acceptance
open Fides.Acceptance.GitHubSimulator

let private github = GitHub.provider GitHub.githubDotCom

let private credentials =
    { ClientId = fixtureClientId
      ClientSecret = Secret.create fixtureClientSecret }

let private verifier: CodeVerifier = Secret.create Scenarios.verifier
let private redirect = Scenarios.chrona.RedirectUris.Head
let private at = Scenarios.epoch

let private respond status body = HttpOutcome.Responded { Status = status; Body = body }

let private grant () =
    let world, code = initial at |> authorize octocat redirect (Pkce.challenge verifier)
    SimulatedHost.run true world (Provider.exchangeCode github credentials (Secret.create code) verifier redirect)

let private ok result =
    match result with
    | Ok value -> value
    | Error e -> failwith $"expected Ok, got {e}"

[<Fact>]
[<Trait("Verifies", "FID-PRV-001")>]
let ``the GitHub provider declares its capabilities and what it proves`` () =
    Assert.Equal<Set<ProviderCapability>>(
        Set.ofList [ AuthorizationCodeWithPkce; ExpiringTokens; RefreshTokens; Revocation; RepositoryAccess ],
        github.Capabilities
    )

    Assert.NotEmpty github.Proves
    Assert.Equal(ProviderId "github", github.Id)

[<Fact>]
[<Trait("Verifies", "FID-PRV-002")>]
let ``the authorization URL asks GitHub for an S256-bound code for the exact redirect URI`` () =
    let url =
        github.AuthorizationUrl
            { ClientId = "Iv23abc"
              RedirectUri = "https://chrona.example/auth/callback"
              State = "st"
              CodeChallenge = "ch" }

    Assert.Equal(
        "https://github.com/login/oauth/authorize?client_id=Iv23abc&redirect_uri=https%3A%2F%2Fchrona.example%2Fauth%2Fcallback&state=st&code_challenge=ch&code_challenge_method=S256",
        url
    )

[<Fact>]
[<Trait("Verifies", "FID-PRV-002")>]
let ``a GitHub App code exchange yields an 8-hour access token and a 6-month refresh token`` () =
    let _, result = grant ()
    let tokens = ok result
    Assert.Equal(at.AddHours 8., tokens.AccessTokenExpiresAt)
    Assert.Equal(at.AddSeconds 15811200., tokens.RefreshTokenExpiresAt)
    Assert.StartsWith("ghu_", Secret.reveal tokens.AccessToken)

[<Fact>]
[<Trait("Verifies", "FID-PRV-002")>]
let ``a refresh rotates the pair through the provider`` () =
    let world, result = grant ()
    let first = ok result
    let _, refreshed = SimulatedHost.run true world (Provider.refresh github credentials first.RefreshToken)
    let second = ok refreshed
    Assert.NotEqual(first.AccessToken, second.AccessToken)
    Assert.NotEqual(first.RefreshToken, second.RefreshToken)

[<Fact>]
[<Trait("Verifies", "FID-PRV-003")>]
let ``the identity comes from GitHub's user endpoint for the token`` () =
    let world, result = grant ()
    let _, identity = SimulatedHost.run true world (Provider.identify github (ok result).AccessToken)

    Assert.Equal(
        Ok
            { Provider = ProviderId "github"
              Subject = "583231"
              Login = "octocat"
              Name = Some "The Octocat" },
        identity
    )

[<Fact>]
let ``revocation and repository access go through the provider`` () =
    let world, result = grant ()
    let token = (ok result).AccessToken
    let repository = RepositoryName.parse "echelon-data/summa" |> ok
    let world, denied = SimulatedHost.run true world (Provider.repositoryAccess github token repository)
    Assert.Equal(Ok NotReadable, denied)
    let world, granted = SimulatedHost.run true (grantRead "echelon-data/summa" octocat world) (Provider.repositoryAccess github token repository)
    Assert.Equal(Ok Readable, granted)
    let world, revoked = SimulatedHost.run true world (Provider.revoke github credentials token)
    Assert.Equal(Ok(), revoked)
    let _, after = SimulatedHost.run true world (Provider.identify github token)
    Assert.Equal(Error TokenRejected, after)

[<Theory>]
[<InlineData("""{"error":"bad_verification_code"}""", "CodeRejected")>]
[<InlineData("""{"error":"redirect_uri_mismatch"}""", "CodeRejected")>]
[<InlineData("""{"error":"bad_refresh_token"}""", "RefreshRejected")>]
[<InlineData("""{"error":"incorrect_client_credentials"}""", "ClientCredentialsRejected")>]
[<InlineData("""{"error":"something_new"}""", "ContractViolation")>]
[<InlineData("""{"access_token":"ghu_x","token_type":"bearer"}""", "ContractViolation")>]
[<InlineData("""{"access_token":"ghu_x","expires_in":28800,"refresh_token":"ghr_x","refresh_token_expires_in":1,"token_type":"mac"}""", "ContractViolation")>]
[<InlineData("""{"access_token":"","expires_in":28800,"refresh_token":"ghr_x","refresh_token_expires_in":1}""", "ContractViolation")>]
[<InlineData("""[1,2]""", "ContractViolation")>]
[<InlineData("""<html>""", "ContractViolation")>]
let ``token responses are interpreted into typed refusals`` (body: string, expected: string) =
    match GitHub.readTokenGrant at (respond 200 body) with
    | Ok _ -> failwith "expected a refusal"
    | Error refusal -> Assert.StartsWith(expected, string refusal)

[<Theory>]
[<InlineData(500)>]
[<InlineData(502)>]
[<InlineData(503)>]
[<InlineData(429)>]
let ``provider failures are outages`` (status: int) =
    Assert.Equal(Error Unavailable, GitHub.readTokenGrant at (respond status "<html>unicorn</html>"))
    Assert.Equal(Error Unavailable, GitHub.readIdentity (respond status ""))
    Assert.Equal(Error Unavailable, GitHub.readRevocation (respond status ""))
    Assert.Equal(Error Unavailable, GitHub.readRepositoryAccess (respond status ""))

[<Fact>]
let ``transport failures are outages`` () =
    for failure in [ HttpOutcome.Failed TimedOut; HttpOutcome.Failed TransportFailure.Unreachable ] do
        Assert.Equal(Error Unavailable, GitHub.readTokenGrant at failure)
        Assert.Equal(Error Unavailable, GitHub.readIdentity failure)

[<Fact>]
let ``a user response without id or login is a contract violation`` () =
    match GitHub.readIdentity (respond 200 """{"login":"octocat"}""") with
    | Error(ContractViolation _) -> ()
    | other -> failwith $"unexpected {other}"

[<Theory>]
[<InlineData("echelon-data/summa", true)>]
[<InlineData("a.b/c_d-e", true)>]
[<InlineData("summa", false)>]
[<InlineData("a/b/c", false)>]
[<InlineData("../summa", false)>]
[<InlineData("a/..", false)>]
[<InlineData("a/b?x=1", false)>]
[<InlineData("a/b%2F..", false)>]
[<InlineData("/b", false)>]
let ``repository names cannot alter the request path`` (value: string, valid: bool) =
    Assert.Equal(valid, Result.isOk (RepositoryName.parse value))

[<Fact>]
let ``provider requests print without their secrets`` () =
    let request = github.CodeExchange credentials (Secret.create "the-code") verifier redirect
    let printed = [ sprintf "%A" request; string request; $"{request}" ]

    for text in printed do
        Assert.DoesNotContain(fixtureClientSecret, text)
        Assert.DoesNotContain("the-code", text)
        Assert.DoesNotContain(Scenarios.verifier, text)
