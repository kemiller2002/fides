/// The acceptance catalogue is complete against issue #1, the trust-boundary
/// threat table and the protocol's codes. Running the scenarios against the
/// exchange core is WI-0007; against the client, WI-0009.
module Fides.Acceptance.AcceptanceCatalogueTests

open System.Text.RegularExpressions
open Xunit
open Fides.Tests
open Fides.Acceptance.Scenarios

let private allIds =
    (exchangeScenarios |> List.map _.Id)
    @ (callbackScenarios |> List.map _.Id)
    @ (sessionScenarios |> List.map _.Id)

let private allCategories =
    (exchangeScenarios |> List.map _.Category)
    @ (callbackScenarios |> List.map _.Category)
    @ (sessionScenarios |> List.map _.Category)

/// Backticked codes in the first column of the Markdown table rows under the
/// heading that contains `heading`.
let tableCodes (heading: string) (markdown: string) =
    let start = markdown.IndexOf heading
    let rest = markdown.Substring start
    let next = rest.IndexOf("\n## ", 1)
    let section = if next > 0 then rest.Substring(0, next) else rest

    Regex.Matches(section, @"^\| `([a-z_]+)`", RegexOptions.Multiline)
    |> Seq.map _.Groups[1].Value
    |> Set.ofSeq

/// The acceptance scenario ids the trust-boundary threat table promises.
let promisedByThreatTable (markdown: string) =
    Regex.Matches(markdown, @"[Aa]cceptance ((?:`[a-z-]+`(?:, )?)+)")
    |> Seq.collect (fun m -> Regex.Matches(m.Groups[1].Value, "`([a-z-]+)`") |> Seq.map _.Groups[1].Value)
    |> Set.ofSeq

let private protocol () = RepositoryFiles.read "docs/architecture/EXCHANGE-PROTOCOL.md"

[<Fact>]
let ``scenario ids are unique`` () =
    Assert.Equal(allIds.Length, (List.distinct allIds).Length)

[<Fact>]
let ``every issue #1 category has an acceptance scenario`` () =
    let missing = requiredCategories |> List.filter (fun c -> not (List.contains c allCategories))
    Assert.Empty missing

[<Fact>]
let ``every acceptance scenario the threat table promises exists`` () =
    let promised = promisedByThreatTable (RepositoryFiles.read "docs/architecture/TRUST-BOUNDARIES.md")
    Assert.NotEmpty promised
    Assert.Empty(Set.difference promised (Set.ofList allIds))

[<Fact>]
let ``every refusal code in the protocol is expected by an exchange scenario`` () =
    let codes = tableCodes "## 3. Refusals" (protocol ())

    let expected =
        exchangeScenarios
        |> List.collect _.Steps
        |> List.choose (fun s ->
            match s.Expect with
            | Refused(_, code) -> Some code
            | Status _ -> None)
        |> Set.ofList

    Assert.Equal(14, codes.Count)
    Assert.Empty(Set.difference codes expected)
    Assert.Empty(Set.difference expected codes)

[<Fact>]
let ``every callback outcome in the protocol is exercised`` () =
    let outcomes = tableCodes "## 5. Client callback outcomes" (protocol ())
    let exercised = callbackScenarios |> List.collect _.Steps |> List.map _.Expect |> Set.ofList
    Assert.Empty(Set.difference (Set.remove "any" outcomes) exercised)

[<Fact>]
let ``every session state and token-provider result is exercised`` () =
    let states = Set.ofList (sessionScenarios |> List.map _.ExpectState)
    let tokens = Set.ofList (sessionScenarios |> List.map _.ExpectToken)
    Assert.Equal<Set<string>>(Set.ofList [ "token"; "none"; "expired"; "revoked"; "provider_unavailable" ], tokens)

    Assert.Equal<Set<string>>(
        Set.ofList [ "signed_in"; "signed_out"; "expired"; "revoked"; "provider_unavailable" ],
        states
    )

[<Fact>]
let ``scenarios cite only declared requirements`` () =
    let declared =
        RequirementsTraceability.declaredRequirements (RepositoryFiles.read "docs/requirements/FIDES-REQUIREMENTS.md")

    let cited =
        (exchangeScenarios |> List.collect _.Requirements)
        @ (callbackScenarios |> List.collect _.Requirements)
        @ (sessionScenarios |> List.collect _.Requirements)
        |> Set.ofList

    Assert.Empty(Set.difference cited declared)

[<Fact>]
let ``scenarios refused before the provider expect a refusal on their only step`` () =
    for scenario in exchangeScenarios |> List.filter (fun s -> not s.ProviderCalled) do
        match scenario.Steps with
        | [ { Expect = Refused _ } ] -> ()
        | _ -> failwith $"{scenario.Id}: a no-provider scenario must be one refused step"
