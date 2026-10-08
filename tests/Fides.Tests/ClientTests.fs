/// Security and behaviour of the client beyond the acceptance scenarios.
module Fides.Tests.ClientTests

open System
open System.Threading.Tasks
open Xunit
open Fides
open Fides.Client
open Fides.Acceptance.Scenarios
open Fides.Tests.FakeBrowser

let private queryOf (url: string) =
    (Uri url).Query.TrimStart('?').Split('&')
    |> Array.map (fun pair ->
        let parts = pair.Split('=', 2)
        Uri.UnescapeDataString parts[0], Uri.UnescapeDataString parts[1])
    |> List.ofArray

let private pendingIn (browser: Browser) =
    Codec.decodePending browser.Tab["fides.chrona-test.pending"] |> Option.get

[<Fact>]
[<Trait("Verifies", "FID-EXC-001")>]
let ``sign-in sends the person to GitHub with a fresh state and an S256 challenge of the stored verifier`` () =
    let browser = Browser(epoch, noExchange)
    let client = FidesClient.create configuration catalog browser.Ports
    Assert.Equal(Ok(), run (client.SignIn MemoryOnly))
    let query = queryOf (Seq.exactlyOne browser.Navigations)
    let pending = pendingIn browser
    Assert.Equal(pending.State, query |> List.find (fst >> (=) "state") |> snd)
    Assert.Equal(Pkce.challenge pending.Verifier, query |> List.find (fst >> (=) "code_challenge") |> snd)
    Assert.Equal("S256", query |> List.find (fst >> (=) "code_challenge_method") |> snd)
    Assert.Equal(configuration.RedirectUri, query |> List.find (fst >> (=) "redirect_uri") |> snd)
    Assert.Equal(43, pending.State.Length)
    Assert.Equal("signing_in", SessionState.code (client.State()))

[<Fact>]
let ``every sign-in has its own state and verifier`` () =
    let browser = Browser(epoch, noExchange)
    let client = FidesClient.create configuration catalog browser.Ports
    run (client.SignIn MemoryOnly) |> ignore
    let first = pendingIn browser
    run (client.SignIn MemoryOnly) |> ignore
    let second = pendingIn browser
    Assert.NotEqual<string>(first.State, second.State)
    Assert.NotEqual(first.Verifier, second.Verifier)

[<Fact>]
let ``the verifier never leaves the tab before the exchange call`` () =
    let browser = Browser(epoch, noExchange)
    let client = FidesClient.create configuration catalog browser.Ports
    run (client.SignIn MemoryOnly) |> ignore
    let verifier = Secret.reveal (pendingIn browser).Verifier
    Assert.DoesNotContain(verifier, Seq.exactlyOne browser.Navigations)
    Assert.Empty browser.Device
    Assert.Empty browser.Broadcasts

[<Fact>]
let ``an unconfigured provider cannot start a sign-in`` () =
    let browser = Browser(epoch, noExchange)
    let client = FidesClient.create { configuration with Provider = ProviderId "gitlab" } catalog browser.Ports
    Assert.True(Result.isError (run (client.SignIn MemoryOnly)))
    Assert.Empty browser.Navigations

let private signedIn (retention: Retention) =
    let browser = Browser(epoch, fun _ _ -> respond 200 (grantBody epoch 480. 260000. "a"))
    let client = FidesClient.create configuration catalog browser.Ports
    run (client.SignIn retention) |> ignore
    let pending = pendingIn browser
    let outcome = run (client.CompleteCallback [ "code", "c"; "state", pending.State ])
    Assert.Equal("signed_in", CallbackOutcome.code outcome)
    browser, client

[<Fact>]
[<Trait("Verifies", "FID-CLI-002")>]
let ``memory-only retention, the default, writes the session to no browser store`` () =
    let browser, _ = signedIn MemoryOnly
    Assert.Empty browser.Tab
    Assert.Empty browser.Device

[<Fact>]
[<Trait("Verifies", "FID-CLI-002")>]
let ``session-scoped retention keeps the session in this tab only`` () =
    let browser, _ = signedIn SessionScoped
    Assert.True(browser.Tab.ContainsKey "fides.chrona-test.session")
    Assert.Empty browser.Device

[<Fact>]
[<Trait("Verifies", "FID-CLI-002")>]
let ``persistent retention needs consent given after a disclosure, and sign-out clears it`` () =
    Assert.True(Result.isError (PersistenceConsent.givenAfter "  "))
    let consent = PersistenceConsent.givenAfter "Keep me signed in on this device for up to 6 months." |> Result.defaultWith (fun () -> failwith "consent")
    let browser, client = signedIn (Persistent consent)
    Assert.True(browser.Device.ContainsKey "fides.chrona-test.session")
    browser.Exchange <- fun _ _ -> respond 204 ""
    Assert.Equal(RevokedAtProvider, run (client.SignOut()))
    Assert.Empty browser.Device
    Assert.Empty browser.Tab
    Assert.Equal("signed_out", SessionState.code (client.State()))

[<Fact>]
[<Trait("Verifies", "FID-TB-005")>]
let ``sign-out clears locally even when revocation fails, and says so`` () =
    let browser, client = signedIn SessionScoped
    browser.Exchange <- fun _ _ -> async { return Failed Unreachable }
    Assert.Equal(RevocationFailed "provider_unavailable", run (client.SignOut()))
    Assert.Empty browser.Tab
    Assert.Equal(Error TokenUnavailable.NoToken, run (client.TokenProvider()))

[<Fact>]
[<Trait("Verifies", "FID-CLI-003")>]
let ``cross-tab messages name what happened and never carry a token`` () =
    let browser, client = signedIn SessionScoped
    browser.Exchange <- fun _ _ -> respond 204 ""
    run (client.SignOut()) |> ignore
    Assert.Equal<string list>([ "fides:chrona-test:signed_in"; "fides:chrona-test:signed_out" ], List.ofSeq browser.Broadcasts)

    for message in browser.Broadcasts do
        Assert.DoesNotContain("ghu_", message)
        Assert.DoesNotContain("ghr_", message)

[<Fact>]
[<Trait("Verifies", "FID-CLI-003")>]
let ``another tab signing out signs this tab out`` () =
    let browser, client = signedIn SessionScoped
    run (client.Receive "fides:chrona-test:signed_out")
    Assert.Equal("signed_out", SessionState.code (client.State()))
    Assert.Empty browser.Tab

[<Fact>]
let ``a message for another application is ignored`` () =
    let _, client = signedIn MemoryOnly
    run (client.Receive "fides:summa-test:signed_out")
    Assert.Equal("signed_in", SessionState.code (client.State()))

[<Fact>]
let ``a callback removes the code and state from the address bar even when refused`` () =
    let browser = Browser(epoch, noExchange)
    let client = FidesClient.create configuration catalog browser.Ports
    run (client.CompleteCallback [ "code", "c"; "state", "forged" ]) |> ignore
    Assert.Equal<string list>([ configuration.RedirectUri ], List.ofSeq browser.Addresses)

[<Fact>]
let ``a pending sign-in from the future is refused as expired`` () =
    let browser = Browser(epoch, noExchange)
    let client = FidesClient.create configuration catalog browser.Ports
    run (client.SignIn MemoryOnly) |> ignore
    let pending = pendingIn browser
    browser.Now <- epoch.AddMinutes -1.
    Assert.Equal(StateExpired, run (client.CompleteCallback [ "code", "c"; "state", pending.State ]))

[<Fact>]
[<Trait("Verifies", "FID-CLI-001")>]
let ``concurrent token requests share one refresh, because refresh tokens are single use`` () =
    let browser, client = signedIn MemoryOnly
    browser.Now <- epoch.AddMinutes 479.
    let gate = TaskCompletionSource<HttpOutcome>()

    browser.Exchange <-
        fun _ _ -> Async.AwaitTask gate.Task

    let first = Async.StartAsTask(client.TokenProvider())
    let second = Async.StartAsTask(client.TokenProvider())
    gate.SetResult(Responded { Status = 200; Body = grantBody browser.Now 480. 260000. "b" })
    Assert.True(Result.isOk first.Result)
    Assert.Equal(first.Result, second.Result)
    Assert.Equal(1, browser.Posted |> Seq.filter (fun (p, _) -> p = Protocol.RefreshPath) |> Seq.length)

[<Fact>]
let ``a refused refresh adopts a session another tab already rotated instead of revoking`` () =
    let consent = PersistenceConsent.givenAfter "Keep me signed in." |> Result.defaultWith (fun () -> failwith "consent")
    let browser, client = signedIn (Persistent consent)
    browser.Now <- epoch.AddMinutes 479.
    // Another tab refreshed first and stored the rotated pair.
    let rotated = Codec.encodeSession { (Codec.decodeSession browser.Device["fides.chrona-test.session"]).Value with Grant = (Protocol.decodeGrant (grantBody browser.Now 480. 260000. "other")).Value |> fst }
    browser.Device["fides.chrona-test.session"] <- rotated
    browser.Exchange <- fun _ _ -> respond 401 (Protocol.encodeRefusal Protocol.RefreshRejected)

    match run (client.TokenProvider()) with
    | Ok token -> Assert.Equal("ghu_fake_other", Secret.reveal token)
    | Error e -> failwith $"expected the other tab's token, got {e}"

    Assert.Equal("signed_in", SessionState.code (client.State()))

[<Fact>]
let ``the stored session and pending sign-in round-trip`` () =
    let consent = PersistenceConsent.givenAfter "d" |> Result.defaultWith (fun () -> failwith "consent")

    let session =
        { Grant = (Protocol.decodeGrant (grantBody epoch 1. 2. "x")).Value |> fst
          Identity = { Provider = ProviderId "github"; Subject = "1"; Login = "o"; Name = None }
          Retention = Persistent consent }

    Assert.Equal(Some session, Codec.decodeSession (Codec.encodeSession session))
    Assert.Equal(None, Codec.decodeSession "{not json")

[<Fact>]
[<Trait("Verifies", "FID-CLI-001")>]
let ``many callers on many threads still make exactly one refresh`` () =
    for _ in 1..20 do
        let browser, client = signedIn MemoryOnly
        browser.Now <- epoch.AddMinutes 479.
        let gate = TaskCompletionSource<HttpOutcome>()
        browser.Exchange <- fun _ _ -> Async.AwaitTask gate.Task
        let callers = Array.init 16 (fun _ -> Async.StartAsTask(client.TokenProvider()))
        Threading.Thread.Sleep 5
        gate.SetResult(Responded { Status = 200; Body = grantBody browser.Now 480. 260000. "many" })
        let results = callers |> Array.map _.Result
        Assert.All(results, fun r -> Assert.True(Result.isOk r))
        Assert.Equal(1, browser.Posted |> Seq.filter (fun (p, _) -> p = Protocol.RefreshPath) |> Seq.length)
