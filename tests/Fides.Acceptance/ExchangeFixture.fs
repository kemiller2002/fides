/// The exchange as the acceptance scenarios see it: configured from the
/// scenarios' registrations through the real configuration parser, and run
/// against the GitHub simulator.
module Fides.Acceptance.ExchangeFixture

open Fides
open Fides.Acceptance.GitHubSimulator
open Fides.Acceptance.Scenarios
open Fides.JsonWrite

let catalog = ProviderCatalog.ofList [ GitHub.provider GitHub.githubDotCom ]

/// A secret-store reference. It is a name, not a secret.
let secretReference = "arn:aws:secretsmanager:us-east-1:000000000000:secret:fides-test-github"

let configurationDocument =
    render (
        Object
            [ "providers", Object [ "github", Object [ "clientId", String fixtureClientId; "clientSecret", String secretReference ] ]
              "applications",
              Array
                  [ for r in registrations ->
                        objectOf
                            [ "id", Some(String r.Application)
                              "provider", Some(String "github")
                              "origins", Some(Array(r.Origins |> List.map String))
                              "redirectUris", Some(Array(r.RedirectUris |> List.map String))
                              "requiredRepository", r.RequiredRepository |> Option.map String ] ] ]
    )

let configuration =
    match Configuration.parse catalog configurationDocument with
    | Ok configuration -> configuration
    | Error problems -> failwith (String.concat "; " problems)

/// The host request a scenario call becomes.
let hostRequest (call: Call) : Service.HostRequest =
    { Method = call.Method
      Path = call.Path
      Headers = [ "Content-Type", "application/json" ] @ (call.Origin |> Option.map (fun o -> [ "Origin", o ]) |> Option.defaultValue [])
      Body = call.Body }
