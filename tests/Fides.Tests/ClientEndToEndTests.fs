/// The client, the exchange core and simulated GitHub together: the whole
/// sign-in, refresh and sign-out a Chrona user would go through.
module Fides.Tests.ClientEndToEndTests

open System
open Xunit
open Fides
open Fides.Client
open Fides.Acceptance
open Fides.Acceptance.GitHubSimulator
open Fides.Acceptance.Scenarios
open Fides.Tests.FakeBrowser

type private World() =
    member val GitHub = initial epoch with get, set

[<Fact>]
[<Trait("Verifies", "FID-CLI-001")>]
[<Trait("Verifies", "FID-PRV-003")>]
let ``a person signs in, Arca gets a token, it refreshes after eight hours, and sign-out revokes it at GitHub`` () =
    let world = World()

    // The exchange core behind HTTP, as the browser would reach it.
    let exchange (path: string) (body: string) =
        async {
            let request: Service.HostRequest =
                { Method = "POST"
                  Path = path
                  Headers = [ "Content-Type", "application/json"; "Origin", "https://chrona.example" ]
                  Body = body }

            let next, (response: Service.HostResponse, _) = SimulatedHost.run true world.GitHub (Service.handle ExchangeFixture.configuration request)
            world.GitHub <- next
            return HttpOutcome.Responded { Status = response.Status; Body = response.Body }
        }

    let browser = Browser(epoch, exchange)
    let client = FidesClient.create configuration catalog browser.Ports

    // 1. Sign in: the person approves at GitHub, which redirects back.
    run (client.SignIn MemoryOnly) |> ignore
    let authorizeUrl = Uri(Seq.exactlyOne browser.Navigations)
    let query = authorizeUrl.Query.TrimStart('?').Split('&') |> Array.map (fun p -> let kv = p.Split('=') in kv[0], Uri.UnescapeDataString kv[1]) |> Map.ofArray
    let next, code = authorize octocat query["redirect_uri"] query["code_challenge"] world.GitHub
    world.GitHub <- next
    let outcome = run (client.CompleteCallback [ "code", code; "state", query["state"] ])

    match outcome with
    | CompletedSignIn identity ->
        Assert.Equal("octocat", identity.Login)
        Assert.Equal("583231", identity.Subject)
    | other -> failwith $"sign-in failed: {other}"

    // 2. Arca asks for a token: the one GitHub issued.
    let first = run (client.TokenProvider()) |> Result.defaultWith (fun e -> failwith $"{e}")
    Assert.True(accessTokenValid (Secret.reveal first) world.GitHub)

    // 3. Eight hours later the token is refreshed through the exchange.
    world.GitHub <- advance (TimeSpan.FromHours 8.) world.GitHub
    browser.Now <- epoch.AddHours 8.
    let second = run (client.TokenProvider()) |> Result.defaultWith (fun e -> failwith $"{e}")
    Assert.NotEqual(first, second)
    Assert.True(accessTokenValid (Secret.reveal second) world.GitHub)

    // 4. Sign-out revokes the grant at GitHub.
    Assert.Equal(RevokedAtProvider, run (client.SignOut()))
    Assert.False(accessTokenValid (Secret.reveal second) world.GitHub)
    Assert.Equal(Error TokenUnavailable.NoToken, run (client.TokenProvider()))
