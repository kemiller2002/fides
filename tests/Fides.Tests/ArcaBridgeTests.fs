/// The bridge against Arca 0.1.0's real port (EchelonFoundry.Arca.Core, from
/// Conditor's verified feed): Arca receives Fides's token or the same typed
/// failure (FID-CLI-001, ARCA-AUTH-001).
module Fides.Tests.ArcaBridgeTests

open Xunit
open Fides
open Fides.Client
open Fides.Arca
open Fides.Acceptance.Scenarios
open Fides.Tests.FakeBrowser

[<Theory>]
[<InlineData("none")>]
[<InlineData("expired")>]
[<InlineData("revoked")>]
[<InlineData("provider_unavailable")>]
[<Trait("Verifies", "FID-CLI-001")>]
let ``every Fides failure is the same Arca failure`` (code: string) =
    let fides, arca =
        match code with
        | "none" -> TokenUnavailable.NoToken, Arca.TokenUnavailable.NoToken
        | "expired" -> TokenUnavailable.Expired, Arca.TokenUnavailable.Expired
        | "revoked" -> TokenUnavailable.Revoked, Arca.TokenUnavailable.Revoked
        | _ -> TokenUnavailable.ProviderFailed "provider_unavailable", Arca.TokenUnavailable.ProviderFailed "provider_unavailable"

    Assert.Equal(arca, TokenBridge.unavailable fides)
    let provider = TokenBridge.toArca (fun () -> async { return Error fides })
    Assert.Equal(Error arca, run (provider ()))

[<Fact>]
[<Trait("Verifies", "FID-CLI-001")>]
let ``Arca sends the Fides token as its bearer credential`` () =
    let provider = TokenBridge.toArca (fun () -> async { return Ok(Secret.create "ghu_bridge_check") })

    match run (provider ()) with
    | Ok token -> Assert.Equal(("Authorization", "Bearer ghu_bridge_check"), Arca.AccessToken.authorization token)
    | Error e -> failwith $"expected a token, got {e}"

[<Fact>]
let ``a token Arca refuses is a provider failure that does not repeat the value`` () =
    let provider = TokenBridge.toArca (fun () -> async { return Ok(Secret.create "has space") })

    match run (provider ()) with
    | Error(Arca.TokenUnavailable.ProviderFailed reason) -> Assert.DoesNotContain("has space", reason)
    | other -> failwith $"unexpected {other}"

[<Fact>]
[<Trait("Verifies", "FID-CLI-001")>]
let ``a signed-in client hands Arca its token, and Arca sees the session end`` () =
    let browser = Browser(epoch, fun _ _ -> respond 200 (grantBody epoch 480. 260000. "arca"))
    let client = FidesClient.create configuration catalog browser.Ports
    let arca = TokenBridge.ofClient client
    Assert.Equal(Error Arca.TokenUnavailable.NoToken, run (arca ()))
    run (client.SignIn MemoryOnly) |> ignore
    let pending = Codec.decodePending browser.Tab["fides.chrona-test.pending"] |> Option.get
    run (client.CompleteCallback [ "code", "c"; "state", pending.State ]) |> ignore

    match run (arca ()) with
    | Ok token ->
        Assert.Equal(("Authorization", "Bearer ghu_fake_arca"), Arca.AccessToken.authorization token)
        Assert.DoesNotContain("ghu_fake_arca", sprintf "%A" token)
    | Error e -> failwith $"expected a token, got {e}"

    run (client.ReportUnauthorized())
    Assert.Equal(Error Arca.TokenUnavailable.Revoked, run (arca ()))
