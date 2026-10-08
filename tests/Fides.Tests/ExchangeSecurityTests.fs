/// Security properties of the exchange beyond the acceptance scenarios:
/// redirect-URI allow-listing, origin and CORS rules, configuration
/// validation, token expiry, fixed refusal bodies, and log hygiene.
module Fides.Tests.ExchangeSecurityTests

open System
open Xunit
open Fides
open Fides.Protocol
open Fides.Tests.Acceptance
open Fides.Tests.Acceptance.GitHubSimulator
open Fides.Tests.Acceptance.Scenarios

let private configuration = ExchangeFixture.configuration
let private chronaApp = configuration.Applications[ApplicationId "chrona-test"]

let private request origin path body : Service.HostRequest =
    { Method = "POST"
      Path = path
      Headers = [ "Content-Type", "application/json" ] @ (origin |> Option.map (fun o -> [ "Origin", o ]) |> Option.defaultValue [])
      Body = body }

let private answer (world: World) (request: Service.HostRequest) =
    SimulatedHost.run true world (Service.handle configuration request)

let private signIn () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)
    answer world (request (Some "https://chrona.example") TokenPath (tokenBody chrona context.Code verifier chrona.RedirectUris.Head))

let private header name (response: Service.HostResponse) =
    response.Headers |> List.tryFind (fun (k, _) -> k = name) |> Option.map snd

[<Theory>]
[<InlineData("https://chrona.example/auth/callback", true)>]
[<InlineData("https://chrona.example/auth/callback/", false)>]
[<InlineData("https://chrona.example/auth/Callback", false)>]
[<InlineData("https://CHRONA.example/auth/callback", false)>]
[<InlineData("https://chrona.example:443/auth/callback", false)>]
[<InlineData("http://chrona.example/auth/callback", false)>]
[<InlineData("https://chrona.example/auth/callback?x=1", false)>]
[<InlineData("https://chrona.example/auth/callback#x", false)>]
[<InlineData("https://chrona.example/auth/callback ", false)>]
[<InlineData(" https://chrona.example/auth/callback", false)>]
[<InlineData("https://chrona.example/auth/../auth/callback", false)>]
[<InlineData("https://chrona.example/auth/%63allback", false)>]
[<InlineData("https://chrona.example.attacker.example/auth/callback", false)>]
[<InlineData("https://chrona.example@attacker.example/auth/callback", false)>]
[<InlineData("https://chrоna.example/auth/callback", false)>] // Cyrillic о
[<Trait("Verifies", "FID-TB-003")>]
let ``redirect URIs are allow-listed by exact ordinal equality`` (candidate: string, allowed: bool) =
    Assert.Equal(allowed, Exchange.redirectAllowed chronaApp candidate)

[<Theory>]
[<InlineData("https://chrona.example", true)>]
[<InlineData("https://chrona.example/", false)>]
[<InlineData("https://chrona.example:443", false)>]
[<InlineData("http://chrona.example", false)>]
[<InlineData("https://summa.example", false)>]
[<InlineData("null", false)>]
[<InlineData("", false)>]
[<Trait("Verifies", "FID-HOST-005")>]
let ``origins are allow-listed exactly per application`` (candidate: string, allowed: bool) =
    Assert.Equal(allowed, Exchange.originAllowed chronaApp (Some candidate))

[<Fact>]
[<Trait("Verifies", "FID-HOST-005")>]
let ``a preflight from a registered origin gets that origin back, never a wildcard or credentials`` () =
    let preflight: Service.HostRequest =
        { Method = "OPTIONS"; Path = TokenPath; Headers = [ "Origin", "https://chrona.example" ]; Body = "" }

    let _, (response, _) = answer (initial epoch) preflight
    Assert.Equal(204, response.Status)
    Assert.Equal(Some "https://chrona.example", header "Access-Control-Allow-Origin" response)
    Assert.Equal(Some "Origin", header "Vary" response)
    Assert.Equal(None, header "Access-Control-Allow-Credentials" response)
    Assert.DoesNotContain(response.Headers, fun (_, v) -> v = "*")

[<Fact>]
[<Trait("Verifies", "FID-HOST-005")>]
let ``a preflight from an unregistered origin gets no CORS headers`` () =
    let preflight: Service.HostRequest =
        { Method = "OPTIONS"; Path = TokenPath; Headers = [ "Origin", "https://attacker.example" ]; Body = "" }

    let _, (response, _) = answer (initial epoch) preflight
    Assert.Equal(403, response.Status)
    Assert.Equal(None, header "Access-Control-Allow-Origin" response)

[<Fact>]
[<Trait("Verifies", "FID-HOST-005")>]
let ``another application's origin gets neither the token nor CORS headers`` () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)
    let body = tokenBody chrona context.Code verifier chrona.RedirectUris.Head
    let world, (response, _) = answer world (request (Some "https://summa.example") TokenPath body)
    Assert.Equal(403, response.Status)
    Assert.Equal(None, header "Access-Control-Allow-Origin" response)
    Assert.Empty world.Requests

[<Fact>]
let ``a body that is not declared as JSON is refused`` () =
    let world, context = signedInAtGitHub chrona octocat (initial epoch)

    let plain =
        { request (Some "https://chrona.example") TokenPath (tokenBody chrona context.Code verifier chrona.RedirectUris.Head) with
            Headers = [ "Content-Type", "text/plain"; "Origin", "https://chrona.example" ] }

    let world, (response, _) = answer world plain
    Assert.Equal(encodeRefusal MalformedRequest, response.Body)
    Assert.Empty world.Requests

[<Fact>]
[<Trait("Verifies", "FID-EXC-001")>]
let ``a sign-in returns absolute expiries from GitHub's lifetimes and is never cached`` () =
    let _, (response, _) = signIn ()
    Assert.Equal(200, response.Status)
    Assert.Equal(Some "2026-10-08T17:00:00Z", Scenarios.field "accessTokenExpiresAt" response.Body)
    Assert.Equal(Some(formatInstant (epoch.AddSeconds 15811200.)), Scenarios.field "refreshTokenExpiresAt" response.Body)
    Assert.Equal(Some "no-store", header "Cache-Control" response)

[<Fact>]
[<Trait("Verifies", "FID-EXC-005")>]
let ``every refusal body is exactly the code, with nothing echoed`` () =
    for refusal in allRefusals do
        Assert.Equal($"{{\"error\":\"{code refusal}\"}}", encodeRefusal refusal)

[<Fact>]
[<Trait("Verifies", "FID-EXC-005")>]
let ``the audit record names only registered applications, so request text cannot reach the log`` () =
    let forged = request (Some "https://chrona.example") TokenPath """{"application":"x\nlevel=admin","code":"c","codeVerifier":"v","redirectUri":"r"}"""
    let _, (_, audit) = answer (initial epoch) forged
    Assert.Equal(None, audit.Application)
    let _, (_, signedIn) = signIn ()
    Assert.Equal(Some "chrona-test", signedIn.Application)
    Assert.Equal("signed_in", signedIn.Outcome)

[<Fact>]
let ``the refusal table in the protocol document matches the code`` () =
    let document = RepositoryFiles.read "docs/architecture/EXCHANGE-PROTOCOL.md"

    for refusal in allRefusals do
        Assert.Contains($"| `{code refusal}` | {status refusal} |", document)

[<Fact>]
let ``a token response round-trips through the protocol codec`` () =
    let grant =
        { AccessToken = Secret.create "a"
          AccessTokenExpiresAt = epoch
          RefreshToken = Secret.create "r"
          RefreshTokenExpiresAt = epoch.AddDays 1. }

    let identity = { Provider = ProviderId "github"; Subject = "1"; Login = "octocat"; Name = None }
    Assert.Equal(Some(grant, Some identity), decodeGrant (encodeGrant grant (Some identity)))
    Assert.Equal(Some(grant, None), decodeGrant (encodeGrant grant None))
