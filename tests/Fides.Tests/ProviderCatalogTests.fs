/// FID-PRV-004: a second provider is a catalog entry. Code that picks the
/// provider by its configured id runs the same operations unchanged.
module Fides.Tests.ProviderCatalogTests

open Xunit
open Fides

/// A provider that is not GitHub, built only from the provider model.
let private example: IdentityProvider =
    let github = GitHub.provider GitHub.githubDotCom

    { github with
        Id = ProviderId "example"
        Capabilities = Set.ofList [ AuthorizationCodeWithPkce ]
        Proves = [ "The person controlled an example.test account." ]
        AuthorizationUrl = fun request -> $"https://example.test/authorize?client_id={request.ClientId}&state={request.State}" }

let private catalog = ProviderCatalog.ofList [ GitHub.provider GitHub.githubDotCom; example ]

/// What an application does with its configuration: look the provider up by
/// id and build the authorization URL. It names no provider.
let private signInUrl (configuredProvider: string) =
    ProviderCatalog.tryFind (ProviderId configuredProvider) catalog
    |> Option.map (fun provider ->
        provider.AuthorizationUrl
            { ClientId = "client"
              RedirectUri = "https://app.example/cb"
              State = "s"
              CodeChallenge = "c" })

[<Fact>]
[<Trait("Verifies", "FID-PRV-004")>]
let ``switching provider is a configuration change`` () =
    Assert.StartsWith("https://github.com/login/oauth/authorize?", (signInUrl "github").Value)
    Assert.StartsWith("https://example.test/authorize?", (signInUrl "example").Value)

[<Fact>]
let ``an unconfigured provider is absent, not defaulted`` () =
    Assert.Equal(None, signInUrl "gitlab")
