module Fides.Tests.ConfigurationTests

open Xunit
open Fides
open Fides.Tests.Acceptance

let private parse (document: string) = Configuration.parse ExchangeFixture.catalog document

let private withApplication (application: string) =
    """{"providers":{"github":{"clientId":"Iv23x","clientSecret":"arn:aws:secretsmanager:r:1:secret:x"}},"applications":["""
    + application
    + "]}"

let private problems document =
    match parse document with
    | Ok _ -> []
    | Error problems -> problems

[<Fact>]
let ``the acceptance fixture's configuration parses`` () =
    Assert.Equal(2, ExchangeFixture.configuration.Applications.Count)

[<Theory>]
[<InlineData("""{"id":"app","provider":"github","origins":["https://app.example/"],"redirectUris":["https://app.example/cb"]}""", "origin")>]
[<InlineData("""{"id":"app","provider":"github","origins":["http://app.example"],"redirectUris":["https://app.example/cb"]}""", "origin")>]
[<InlineData("""{"id":"app","provider":"github","origins":["https://*.example"],"redirectUris":["https://app.example/cb"]}""", "origin")>]
[<InlineData("""{"id":"app","provider":"github","origins":["https://app.example"],"redirectUris":["https://app.example/*"]}""", "redirect URI")>]
[<InlineData("""{"id":"app","provider":"github","origins":["https://app.example"],"redirectUris":["https://app.example/cb#x"]}""", "redirect URI")>]
[<InlineData("""{"id":"app","provider":"github","origins":["https://app.example"],"redirectUris":["https://user@app.example/cb"]}""", "redirect URI")>]
[<InlineData("""{"id":"app","provider":"github","origins":["https://app.example"],"redirectUris":["http://localhost:5000/cb"]}""", "redirect URI")>]
[<InlineData("""{"id":"app","provider":"github","origins":["https://app.example"],"redirectUris":["/cb"]}""", "redirect URI")>]
[<InlineData("""{"id":"app","provider":"github","origins":[],"redirectUris":["https://app.example/cb"]}""", "origin is required")>]
[<InlineData("""{"id":"app","provider":"github","origins":["https://app.example"],"redirectUris":[]}""", "redirect URI is required")>]
[<InlineData("""{"id":"App!","provider":"github","origins":["https://app.example"],"redirectUris":["https://app.example/cb"]}""", "id must be")>]
[<InlineData("""{"id":"app","provider":"gitlab","origins":["https://app.example"],"redirectUris":["https://app.example/cb"]}""", "not available")>]
[<InlineData("""{"id":"app","provider":"github","origins":["https://app.example"],"redirectUris":["https://app.example/cb"],"requiredRepository":"../x"}""", "requiredRepository")>]
[<Trait("Verifies", "FID-TB-003")>]
let ``unsafe registrations are refused with a reason`` (application: string, reason: string) =
    Assert.Contains(problems (withApplication application), fun p -> p.Contains reason)

[<Fact>]
let ``loopback redirects and origins are allowed only when the configuration opts in`` () =
    let app = """{"id":"app","provider":"github","origins":["http://localhost:5000"],"redirectUris":["http://localhost:5000/cb"]}"""
    Assert.NotEmpty(problems (withApplication app))
    Assert.Empty(problems ((withApplication app).Replace("{\"providers\"", "{\"allowLoopback\":true,\"providers\"")))

[<Fact>]
let ``every problem is reported, not only the first`` () =
    let app = """{"id":"app","provider":"github","origins":["http://a"],"redirectUris":["ftp://b"]}"""
    Assert.Equal(2, (problems (withApplication app)).Length)

[<Fact>]
let ``a provider without a secret reference is refused`` () =
    let document = """{"providers":{"github":{"clientId":"Iv23x"}},"applications":[]}"""
    Assert.Contains(problems document, fun p -> p.Contains "clientSecret")

[<Fact>]
let ``an application registered twice is refused`` () =
    let app = """{"id":"app","provider":"github","origins":["https://a.example"],"redirectUris":["https://a.example/cb"]}"""
    let document = (withApplication app).Replace("[" + app + "]", "[" + app + "," + app + "]")
    Assert.Contains(problems document, fun p -> p.Contains "registered twice")

[<Fact>]
let ``the configuration holds a secret reference, never a secret`` () =
    let client = ExchangeFixture.configuration.Clients[ProviderId "github"]
    Assert.Equal(SecretReference ExchangeFixture.secretReference, client.ClientSecret)
