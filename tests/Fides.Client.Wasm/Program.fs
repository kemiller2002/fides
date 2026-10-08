/// The client's whole life inside WebAssembly: sign-in with PKCE (SHA-256 in
/// the wasm runtime), the callback, the exchange core and simulated GitHub,
/// the token provider, a refresh and sign-out. Prints one verdict line.
module Fides.Client.Wasm.Program

open System
open System.Collections.Generic
open Fides
open Fides.Client
open Fides.Acceptance
open Fides.Acceptance.GitHubSimulator
open Fides.Acceptance.Scenarios

/// Runs an async that completes without waiting: the browser runtime has one
/// thread, so blocking would deadlock. Every port here answers immediately.
let private now' (work: Async<'a>) =
    let task = Async.StartImmediateAsTask work

    if task.IsCompleted then
        task.Result
    else
        failwith "an operation did not complete synchronously"

let private check (name: string) (condition: bool) =
    if not condition then
        failwith $"check failed: {name}"

let private flow () =
    let mutable github = initial epoch
    let mutable clock = epoch
    let tab = Dictionary<string, string>()
    let device = Dictionary<string, string>()
    let navigations = List<string>()

    let store (d: Dictionary<string, string>) =
        { Read = fun k -> async { return (match d.TryGetValue k with | true, v -> Some v | _ -> None) }
          Write = fun k v -> async { d[k] <- v }
          Remove = fun k -> async { d.Remove k |> ignore } }

    let exchange (path: string) (body: string) =
        async {
            let request: Service.HostRequest =
                { Method = "POST"
                  Path = path
                  Headers = [ "Content-Type", "application/json"; "Origin", "https://chrona.example" ]
                  Body = body }

            let next, (response: Service.HostResponse, _) = SimulatedHost.run true github (Service.handle ExchangeFixture.configuration request)
            github <- next
            return HttpOutcome.Responded { Status = response.Status; Body = response.Body }
        }

    let random = Random(7)

    let ports =
        { PostToExchange = exchange
          TabStorage = store tab
          DeviceStorage = store device
          Navigate = fun url -> async { navigations.Add url }
          ReplaceAddress = fun _ -> async { () }
          Broadcast = ignore
          Now = fun () -> clock
          RandomBytes = fun n -> Array.init n (fun _ -> byte (random.Next 256)) }

    let configuration =
        { Application = "chrona-test"
          Provider = ProviderId "github"
          ClientId = fixtureClientId
          RedirectUri = "https://chrona.example/auth/callback" }

    let client = FidesClient.create configuration (ProviderCatalog.ofList [ GitHub.provider GitHub.githubDotCom ]) ports

    check "sign-in starts" (now' (client.SignIn MemoryOnly) = Ok())
    let url = Uri(navigations[0])

    let query =
        url.Query.TrimStart('?').Split('&')
        |> Array.map (fun p -> let kv = p.Split('=') in kv[0], Uri.UnescapeDataString kv[1])
        |> Map.ofArray

    let next, code = authorize octocat query["redirect_uri"] query["code_challenge"] github
    github <- next

    match now' (client.CompleteCallback [ "code", code; "state", query["state"] ]) with
    | CompletedSignIn identity -> check "identity from GitHub" (identity.Login = "octocat")
    | other -> failwith $"sign-in failed: {CallbackOutcome.code other}"

    let first = now' (client.TokenProvider())
    check "token issued" (Result.isOk first)
    github <- advance (TimeSpan.FromHours 8.) github
    clock <- epoch.AddHours 8.
    let second = now' (client.TokenProvider())
    check "token refreshed" (Result.isOk second && second <> first)
    check "replayed callback refused" (now' (client.CompleteCallback [ "code", code; "state", query["state"] ]) = StateInvalid)
    check "sign-out revokes" (now' (client.SignOut()) = RevokedAtProvider)
    check "no token after sign-out" (now' (client.TokenProvider()) = Error TokenUnavailable.NoToken)

[<EntryPoint>]
let main _ =
    try
        flow ()
        printfn "FIDES-WASM-OK runtime=%s" (Runtime.InteropServices.RuntimeInformation.OSDescription)
        0
    with error ->
        printfn "FIDES-WASM-FAILED %s" (string error)
        1
