/// The requirements catalogue as data (WI-0022). Every requirement in
/// `docs/requirements/FIDES-REQUIREMENTS.md` is a record with a fixed set of
/// fields. This module parses the records, checks that each one is complete
/// and internally consistent, checks its declared status against the tests
/// and the open work that actually exist, and renders the traceability
/// matrix. Everything here is pure: the tests supply the file contents.
module Fides.Tests.RequirementsCatalogue

open System
open System.Text.RegularExpressions

/// The fields every requirement record carries, in the order they are written.
let fieldNames =
    [ "Priority"
      "Owner"
      "Status"
      "Change"
      "Source"
      "Rationale"
      "Depends on"
      "Acceptance"
      "Positive test"
      "Negative test"
      "Evidence"
      "Levels achieved"
      "Levels required" ]

let priorities = set [ "P0"; "P1"; "P2"; "P3" ]

/// Implementation status (mission phase 5).
let statuses =
    set [ "implemented-verified"; "implemented-unverified"; "missing"; "blocked"; "deferred" ]

/// Whether the requirement was kept as it was, revised, or added by WI-0022.
let changes = set [ "retained"; "revised"; "added" ]

/// Who owns the requirement. Fides packages own what Fides enforces; the
/// other owners record that a requirement's enforcement point is outside
/// Fides, which the requirement must then say.
let owners =
    set
        [ "Fides.Core"
          "Fides.Client"
          "Fides.Hosting"
          "Fides.Hosting.Aws"
          "Fides.Arca"
          "Fides.UI"
          "Fides.Governance"
          "Fides.Release"
          "Application"
          "Arca"
          "Strata"
          "Limen"
          "Forma"
          "Aegis"
          "Operator"
          "Owner decision" ]

/// Kinds of verification. `simulated-provider` runs against the GitHub
/// simulator; `emulated-host` runs the deployed artefact against an emulated
/// platform; `live-provider` runs against real GitHub. A property of the real
/// provider is never verified by simulation (mission phase 4).
let levels =
    set [ "document"; "unit"; "integration"; "simulated-provider"; "emulated-host"; "browser-wasm"; "live-provider"; "manual-accessibility"; "none" ]

/// Requirement families whose P0 requirements need an adversarial test.
let securityFamilies =
    set [ "TB"; "EXC"; "IDN"; "SES"; "AUZ"; "DATA"; "ORG"; "OPS"; "SVC" ]

type Requirement =
    { Id: string
      Family: string
      Statement: string
      Fields: Map<string, string> }

    member this.Field name =
        this.Fields.TryFind name |> Option.defaultValue ""

let private recordStart = Regex(@"^\*\*(FID-([A-Z]+)-\d{3})\*\*\s*(.*)$")
let private fieldLine = Regex(@"^- ([A-Za-z ]+): (.*)$")

/// Splits the document into requirement records. A record starts at a line
/// `**FID-XXX-NNN** statement` and ends at the next record or heading.
let parse (document: string) : Requirement list =
    let lines = document.Replace("\r\n", "\n").Split('\n') |> List.ofArray

    let close (current: Requirement option) (records: Requirement list) =
        match current with
        | Some record -> record :: records
        | None -> records

    let step (current: Requirement option, records: Requirement list) (line: string) =
        let start = recordStart.Match line

        if start.Success then
            Some
                { Id = start.Groups[1].Value
                  Family = start.Groups[2].Value
                  Statement = start.Groups[3].Value.Trim()
                  Fields = Map.empty },
            close current records
        elif line.StartsWith "#" then
            None, close current records
        else
            match current with
            | None -> None, records
            | Some record ->
                let field = fieldLine.Match line

                if field.Success && List.contains field.Groups[1].Value fieldNames then
                    Some { record with Fields = record.Fields.Add(field.Groups[1].Value, field.Groups[2].Value.Trim()) }, records
                elif record.Fields.IsEmpty && line.Trim() <> "" then
                    // The statement may continue over several lines before the fields.
                    Some { record with Statement = record.Statement + " " + line.Trim() }, records
                else
                    current, records

    let last, records = lines |> List.fold step (None, [])
    close last records |> List.rev

/// Splits a comma-separated field into its trimmed values.
let values (text: string) =
    text.Split(',', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries) |> List.ofArray

/// Requirement IDs a field names, such as `Depends on`.
let referencedIds (text: string) =
    Regex.Matches(text, @"FID-[A-Z]+-\d{3}") |> Seq.map _.Value |> Set.ofSeq

/// Why a record fails the catalogue's rules.
type Finding =
    | DuplicateId of id: string
    | MissingField of id: string * field: string
    | EmptyStatement of id: string
    | NotNormative of id: string
    | UnknownValue of id: string * field: string * value: string
    | UnknownDependency of id: string * dependency: string
    | SelfDependency of id: string
    | VerifiedWithoutTest of id: string
    | VerifiedBelowRequiredLevel of id: string * missing: string
    | ClaimedByTestButMissing of id: string * status: string
    | AchievedButNotImplemented of id: string * status: string
    | ImplementedWithoutEvidenceLevel of id: string
    | OpenButUnplanned of id: string * status: string
    | NoAdversarialTest of id: string

/// Structural findings: every field present, values from the allowed sets,
/// dependencies that name declared requirements, a normative statement.
let structuralFindings (requirements: Requirement list) =
    let declared = requirements |> List.map _.Id |> Set.ofList

    let duplicates =
        requirements
        |> List.countBy _.Id
        |> List.filter (fun (_, n) -> n > 1)
        |> List.map (fst >> DuplicateId)

    let perRecord (r: Requirement) =
        let missing =
            fieldNames
            |> List.filter (fun name -> String.IsNullOrWhiteSpace(r.Field name))
            |> List.map (fun name -> MissingField(r.Id, name))

        let oneOf field allowed =
            let value = r.Field field
            if value = "" || Set.contains value allowed then [] else [ UnknownValue(r.Id, field, value) ]

        let allOf field allowed =
            values (r.Field field)
            |> List.filter (fun v -> not (Set.contains v allowed))
            |> List.map (fun v -> UnknownValue(r.Id, field, v))

        let dependencies = referencedIds (r.Field "Depends on")

        let statement =
            if String.IsNullOrWhiteSpace r.Statement then [ EmptyStatement r.Id ]
            elif not (Regex.IsMatch(r.Statement, @"\b(MUST|SHOULD|MAY)\b")) then [ NotNormative r.Id ]
            else []

        let self = if dependencies.Contains r.Id then [ SelfDependency r.Id ] else []

        let dangling =
            Set.difference dependencies declared |> Set.toList |> List.map (fun d -> UnknownDependency(r.Id, d))

        List.concat
            [ missing
              statement
              oneOf "Priority" priorities
              oneOf "Status" statuses
              oneOf "Change" changes
              allOf "Owner" owners
              allOf "Levels achieved" levels
              allOf "Levels required" levels
              self
              dangling ]

    duplicates @ (requirements |> List.collect perRecord)

/// Status findings: a declared status must agree with what the repository
/// shows. `verified` is the set of requirement IDs some test carries a
/// `Verifies` trait for; `planned` is the set named by open work items.
let statusFindings (verified: Set<string>) (planned: Set<string>) (requirements: Requirement list) =
    let perRecord (r: Requirement) =
        let status = r.Field "Status"
        let achieved = values (r.Field "Levels achieved") |> List.filter ((<>) "none") |> Set.ofList
        let required = values (r.Field "Levels required") |> List.filter ((<>) "none") |> Set.ofList
        let claimed = verified.Contains r.Id
        let finding condition value = if condition then [ value ] else []

        let byStatus =
            match status with
            | "implemented-verified" ->
                List.concat
                    [ finding (not claimed) (VerifiedWithoutTest r.Id)
                      Set.difference required achieved |> Set.toList |> List.map (fun level -> VerifiedBelowRequiredLevel(r.Id, level))
                      finding achieved.IsEmpty (ImplementedWithoutEvidenceLevel r.Id) ]
            | "missing"
            | "blocked"
            | "deferred" ->
                List.concat
                    [ finding claimed (ClaimedByTestButMissing(r.Id, status))
                      finding (not achieved.IsEmpty) (AchievedButNotImplemented(r.Id, status)) ]
            | _ -> []

        let unplanned =
            finding (status <> "implemented-verified" && status <> "" && not (planned.Contains r.Id)) (OpenButUnplanned(r.Id, status))

        let adversarial =
            finding
                (r.Field "Priority" = "P0"
                 && securityFamilies.Contains r.Family
                 && (r.Field "Negative test").StartsWith("n/a", StringComparison.OrdinalIgnoreCase))
                (NoAdversarialTest r.Id)

        List.concat [ byStatus; unplanned; adversarial ]

    requirements |> List.collect perRecord

/// A short form of a field for the matrix: the first sentence, no pipes.
let private cell (text: string) =
    let flat = text.Replace("|", "/").Replace("\n", " ").Trim()
    if flat.Length > 140 then flat.Substring(0, 137).TrimEnd() + "..." else flat

let private familyNames =
    Map.ofList
        [ "CTX", "Repository context"
          "TB", "Trust boundaries"
          "PRV", "Provider model"
          "EXC", "Code-for-token exchange"
          "HOST", "Hosting"
          "CLI", "Client library"
          "TEST", "Verification obligations"
          "IDN", "A. Identity and authentication"
          "SES", "A. Sessions"
          "SVC", "A. Non-human identities"
          "AUZ", "B. Authorization"
          "DATA", "C. Data-layer security"
          "ORG", "D. Multi-organization"
          "UX", "E. Application integration and UX"
          "OPS", "F. Operational security"
          "REL", "G. Reliability and performance" ]

/// The order families appear in the matrix.
let familyOrder =
    [ "CTX"; "TB"; "PRV"; "EXC"; "HOST"; "CLI"; "TEST"; "IDN"; "SES"; "SVC"; "AUZ"; "DATA"; "ORG"; "UX"; "OPS"; "REL" ]

let private familyRank family =
    familyOrder |> List.tryFindIndex ((=) family) |> Option.defaultValue familyOrder.Length

/// Counts of requirements per (row, column) for the summary tables.
let private countTable (rowName: string) (rows: string list) (rowOf: Requirement -> string) (columns: string list) (columnOf: Requirement -> string) (requirements: Requirement list) =
    let header = $"| {rowName} | " + String.Join(" | ", columns) + " | Total |"
    let rule = "|---|" + String.replicate (columns.Length + 1) "---:|"

    let line row =
        let inRow = requirements |> List.filter (fun r -> rowOf r = row)
        let counts = columns |> List.map (fun c -> inRow |> List.filter (fun r -> columnOf r = c) |> List.length |> string)
        $"| {row} | " + String.Join(" | ", counts) + $" | {inRow.Length} |"

    let total =
        let counts = columns |> List.map (fun c -> requirements |> List.filter (fun r -> columnOf r = c) |> List.length |> string)
        "| **Total** | " + String.Join(" | ", counts) + $" | {requirements.Length} |"

    [ header; rule ] @ (rows |> List.filter (fun row -> requirements |> List.exists (fun r -> rowOf r = row)) |> List.map line) @ [ total ]

let statusOrder =
    [ "implemented-verified"; "implemented-unverified"; "missing"; "blocked"; "deferred" ]

/// The requirement-to-implementation-to-test matrix, generated from the
/// catalogue so it cannot drift from it.
let renderMatrix (requirements: Requirement list) =
    let familyLabel (r: Requirement) =
        familyNames.TryFind r.Family |> Option.defaultValue r.Family

    let ordered = requirements |> List.sortBy (fun r -> familyRank r.Family, r.Id)
    let familyRows = familyOrder |> List.choose familyNames.TryFind

    let rows =
        ordered
        |> List.map (fun r ->
            let columns =
                [ r.Id
                  r.Field "Priority"
                  r.Field "Owner"
                  r.Field "Status"
                  r.Field "Change"
                  cell (r.Field "Evidence")
                  r.Field "Levels achieved"
                  r.Field "Levels required" ]

            "| " + String.Join(" | ", columns) + " |")

    [ "<!-- Generated from docs/requirements/FIDES-REQUIREMENTS.md by tests/Fides.Tests/RequirementsCatalogue.fs. Do not edit by hand: run the tests with FIDES_REGENERATE_MATRIX=1. -->"
      ""
      "## Summary by family and status"
      ""
      yield! countTable "Family" familyRows familyLabel statusOrder (fun r -> r.Field "Status") ordered
      ""
      "## Summary by family and priority"
      ""
      yield! countTable "Family" familyRows familyLabel [ "P0"; "P1"; "P2"; "P3" ] (fun r -> r.Field "Priority") ordered
      ""
      "## Summary by change"
      ""
      yield! countTable "Family" familyRows familyLabel [ "retained"; "revised"; "added" ] (fun r -> r.Field "Change") ordered
      ""
      "## Requirement to implementation to test"
      ""
      "| Requirement | Priority | Owner | Status | Change | Evidence (implementation and tests) | Levels achieved | Levels required |"
      "|---|---|---|---|---|---|---|---|"
      yield! rows ]
    |> String.concat "\n"

let private matrixStart = "<!-- matrix:start -->"
let private matrixEnd = "<!-- matrix:end -->"

/// The generated part of a matrix document, between its markers.
let generatedSection (document: string) =
    let s = document.IndexOf matrixStart
    let e = document.IndexOf matrixEnd

    if s < 0 || e < s then None
    else Some(document.Substring(s + matrixStart.Length, e - s - matrixStart.Length).Trim())

/// The matrix document with its generated part replaced.
let withGeneratedSection (document: string) (generated: string) =
    let s = document.IndexOf matrixStart
    let e = document.IndexOf matrixEnd

    if s < 0 || e < s then
        document
    else
        document.Substring(0, s + matrixStart.Length) + "\n" + generated + "\n" + document.Substring e
