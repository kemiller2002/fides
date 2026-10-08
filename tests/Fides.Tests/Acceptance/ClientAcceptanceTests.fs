/// Every callback and session acceptance scenario from WI-0005, run against
/// the client (FID-TEST-001).
module Fides.Tests.Acceptance.ClientAcceptanceTests

open System
open Xunit
open Fides
open Fides.Client
open Fides.Acceptance.Scenarios
open Fides.Tests.FakeBrowser

let callbackIds: seq<objnull array> = callbackScenarios |> Seq.map (fun s -> [| box s.Id |])
let sessionIds: seq<objnull array> = sessionScenarios |> Seq.map (fun s -> [| box s.Id |])

let private exchangeAnswering (answer: string) =
    fun (_: string) (_: string) ->
        if answer = "signed_in" then
            respond 200 (grantBody epoch 480. 260000. "cb")
        else
            let refusal = (Protocol.ofCode answer).Value
            respond (Protocol.status refusal) (Protocol.encodeRefusal refusal)

[<Theory>]
[<MemberData(nameof callbackIds)>]
[<Trait("Verifies", "FID-TEST-001")>]
[<Trait("Verifies", "FID-CLI-001")>]
[<Trait("Verifies", "FID-CLI-003")>]
[<Trait("Verifies", "FID-EXC-001")>]
[<Trait("Verifies", "FID-EXC-003")>]
let ``the client satisfies the callback scenario`` (id: string) =
    let scenario = callbackScenarios |> List.find (fun s -> s.Id = id)
    let browser = Browser(epoch, exchangeAnswering scenario.ExchangeAnswer)

    match scenario.Pending with
    | Some pending ->
        let verifier = (Pkce.verifierFrom (Array.init 32 byte)) |> Result.defaultWith (fun () -> failwith "verifier")

        browser.Tab["fides.chrona-test.pending"] <-
            Codec.encodePending
                { State = pending.State
                  Verifier = verifier
                  CreatedAt = epoch.AddMinutes(float -pending.AgeMinutes)
                  Retention = MemoryOnly }
    | None -> ()

    let client = FidesClient.create configuration catalog browser.Ports

    for step in scenario.Steps do
        let before = browser.Posted.Count
        let outcome = run (client.CompleteCallback step.Query)
        Assert.Equal(step.Expect, CallbackOutcome.code outcome)
        Assert.Equal(step.ExchangeCalled, browser.Posted.Count > before)
        Assert.False(browser.Tab.ContainsKey "fides.chrona-test.pending", "the pending sign-in must be consumed")
        Assert.Equal(configuration.RedirectUri, Seq.last browser.Addresses)

[<Theory>]
[<MemberData(nameof sessionIds)>]
[<Trait("Verifies", "FID-TEST-001")>]
[<Trait("Verifies", "FID-CLI-001")>]
[<Trait("Verifies", "FID-CLI-002")>]
[<Trait("Verifies", "FID-CLI-003")>]
[<Trait("Verifies", "FID-TB-005")>]
let ``the client satisfies the session scenario`` (id: string) =
    let scenario = sessionScenarios |> List.find (fun s -> s.Id = id)

    let exchange (path: string) (_: string) =
        match path, scenario.Refresh with
        | p, RefreshSucceeds when p = Protocol.RefreshPath -> respond 200 (grantBody epoch 480. 260000. "renewed")
        | p, RefreshRefused when p = Protocol.RefreshPath -> respond 401 (Protocol.encodeRefusal Protocol.RefreshRejected)
        | p, RefreshUnavailable when p = Protocol.RefreshPath -> respond 503 (Protocol.encodeRefusal Protocol.ProviderUnavailable)
        | p, _ when p = Protocol.RevokePath -> respond 204 ""
        | p, _ -> failwith $"unexpected exchange call {p}"

    let browser = Browser(epoch, exchange)

    match scenario.Session with
    | Session(access, refresh) ->
        browser.Tab["fides.chrona-test.session"] <-
            Codec.encodeSession
                { Grant = (Protocol.decodeGrant (grantBody epoch (float access) (float refresh) "stored")).Value |> fst
                  Identity = { Provider = ProviderId "github"; Subject = "583231"; Login = "octocat"; Name = None }
                  Retention = SessionScoped }
    | NoSession -> ()

    let client = FidesClient.create configuration catalog browser.Ports
    run (client.Restore()) |> ignore

    for event in scenario.Events do
        match event with
        | ApplicationReportedUnauthorized -> run (client.ReportUnauthorized())
        | SignOut -> run (client.SignOut()) |> ignore

    let token = run (client.TokenProvider())

    let tokenCode =
        match token with
        | Ok _ -> "token"
        | Error reason -> TokenUnavailable.code reason

    Assert.Equal(scenario.ExpectToken, tokenCode)
    Assert.Equal(scenario.ExpectState, SessionState.code (client.State()))

    if scenario.Refresh = RefreshNotExpected then
        Assert.DoesNotContain(browser.Posted, fun (p, _) -> p = Protocol.RefreshPath)
