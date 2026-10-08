/// A browser for the client's tests: in-memory stores, a recorded address
/// bar, recorded messages, a settable clock and deterministic randomness.
module Fides.Tests.FakeBrowser

open System
open System.Collections.Generic
open Fides
open Fides.Client

type Browser(now: DateTimeOffset, exchange: string -> string -> Async<HttpOutcome>) =
    let tab = Dictionary<string, string>()
    let device = Dictionary<string, string>()
    let posted = List<string * string>()
    let navigations = List<string>()
    let addresses = List<string>()
    let broadcasts = List<string>()
    let mutable clock = now
    let mutable exchange = exchange
    let mutable counter = 0uy

    let store (d: Dictionary<string, string>) =
        { Read = fun k -> async { return (match d.TryGetValue k with | true, v -> Some v | _ -> None) }
          Write = fun k v -> async { d[k] <- v }
          Remove = fun k -> async { d.Remove k |> ignore } }

    member _.Tab = tab
    member _.Device = device
    member _.Posted = posted
    member _.Navigations = navigations
    member _.Addresses = addresses
    member _.Broadcasts = broadcasts

    member _.Now
        with get () = clock
        and set value = clock <- value

    member _.Exchange
        with set value = exchange <- value

    member this.Ports: ClientPorts =
        { PostToExchange =
            fun path body ->
                posted.Add((path, body))
                exchange path body
          TabStorage = store tab
          DeviceStorage = store device
          Navigate = fun url -> async { navigations.Add url }
          ReplaceAddress = fun url -> async { addresses.Add url }
          Broadcast = broadcasts.Add
          Now = fun () -> clock
          RandomBytes =
            fun n ->
                counter <- counter + 1uy
                Array.init n (fun i -> byte i + counter) }

let configuration: ClientConfiguration =
    { Application = "chrona-test"
      Provider = ProviderId "github"
      ClientId = Fides.Acceptance.GitHubSimulator.fixtureClientId
      RedirectUri = "https://chrona.example/auth/callback" }

let catalog = ProviderCatalog.ofList [ GitHub.provider GitHub.githubDotCom ]

let respond status body = async { return Responded { Status = status; Body = body } }

let noExchange: string -> string -> Async<HttpOutcome> =
    fun path _ -> failwith $"the exchange must not be called ({path})"

/// A token response the exchange would send.
let grantBody (now: DateTimeOffset) (accessMinutes: float) (refreshMinutes: float) (suffix: string) =
    Protocol.encodeGrant
        { AccessToken = Secret.create ("ghu_fake_" + suffix)
          AccessTokenExpiresAt = now.AddMinutes accessMinutes
          RefreshToken = Secret.create ("ghr_fake_" + suffix)
          RefreshTokenExpiresAt = now.AddMinutes refreshMinutes }
        (Some { Provider = ProviderId "github"; Subject = "583231"; Login = "octocat"; Name = Some "The Octocat" })

let run (work: Async<'a>) = Async.RunSynchronously work
