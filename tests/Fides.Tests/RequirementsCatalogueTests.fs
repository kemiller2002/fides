/// The requirements baseline is complete, internally consistent and honest
/// about its status (WI-0022, FID-TEST-003..005). A requirement that claims
/// to be verified must have a test that says so, at every verification level
/// the requirement needs; a requirement that is not verified must be planned
/// by open work; the traceability matrix is generated from the catalogue.
module Fides.Tests.RequirementsCatalogueTests

open System
open Xunit
open Fides.Tests.RequirementsCatalogue

let private requirementsPath = "docs/requirements/FIDES-REQUIREMENTS.md"
let private matrixPath = "docs/requirements/TRACEABILITY-MATRIX.md"

let private catalogue () = parse (RepositoryFiles.read requirementsPath)

let private testSources () =
    IO.Directory.EnumerateFiles(IO.Path.Combine(RepositoryFiles.root (), "tests"), "*.fs", IO.SearchOption.AllDirectories)
    |> Seq.filter (fun path -> not (path.Contains "/obj/" || path.Contains "/bin/"))
    |> Seq.map IO.File.ReadAllText
    |> List.ofSeq

let private verified () =
    RequirementsTraceability.verifiedInSources (testSources ())

let private planned () =
    RequirementsTraceability.namedRequirements (RequirementsTraceability.openWorkText (RepositoryFiles.read ".ros/work/queue.json"))

[<Fact>]
[<Trait("Verifies", "FID-TEST-003")>]
let ``every requirement record parses with the same IDs the traceability gate sees`` () =
    let document = RepositoryFiles.read requirementsPath
    let parsed = parse document |> List.map _.Id |> Set.ofList
    Assert.Equal<Set<string>>(RequirementsTraceability.declaredRequirements document, parsed)

[<Fact>]
[<Trait("Verifies", "FID-TEST-003")>]
let ``every requirement has every field with an allowed value`` () =
    Assert.Empty(structuralFindings (catalogue ()))

[<Fact>]
[<Trait("Verifies", "FID-TEST-004")>]
let ``every requirement's status agrees with the tests and open work that exist`` () =
    Assert.Empty(statusFindings (verified ()) (planned ()) (catalogue ()))

[<Fact>]
[<Trait("Verifies", "FID-TEST-005")>]
let ``the traceability matrix is generated from the catalogue`` () =
    let generated = renderMatrix (catalogue ())
    let matrix = RepositoryFiles.read matrixPath

    if Environment.GetEnvironmentVariable "FIDES_REGENERATE_MATRIX" = "1" then
        IO.File.WriteAllText(IO.Path.Combine(RepositoryFiles.root (), matrixPath), withGeneratedSection matrix generated)
    else
        Assert.Equal(Some generated, generatedSection matrix)

// The rules themselves, against small synthetic catalogues.

let private record (id: string) (fields: (string * string) list) =
    let defaults =
        Map.ofList
            [ "Priority", "P0"
              "Owner", "Fides.Core"
              "Status", "missing"
              "Change", "added"
              "Source", "test"
              "Rationale", "test"
              "Depends on", "none"
              "Acceptance", "test"
              "Positive test", "test"
              "Negative test", "test"
              "Evidence", "none"
              "Levels achieved", "none"
              "Levels required", "unit" ]

    let merged = fields |> List.fold (fun (m: Map<string, string>) (k, v) -> m.Add(k, v)) defaults
    let lines = fieldNames |> List.map (fun name -> $"- {name}: {merged[name]}")
    String.concat "\n" ([ $"**{id}** The system MUST do it." ; "" ] @ lines)

[<Fact>]
let ``a record is parsed with its statement and fields`` () =
    let parsed = parse ("# Title\n\n" + record "FID-AUZ-001" [ "Priority", "P1" ] + "\n\n## Next\n")
    let r = Assert.Single parsed
    Assert.Equal("FID-AUZ-001", r.Id)
    Assert.Equal("AUZ", r.Family)
    Assert.Equal("The system MUST do it.", r.Statement)
    Assert.Equal("P1", r.Field "Priority")

[<Fact>]
let ``a record missing a field, with an unknown status or a dangling dependency is reported`` () =
    let text =
        (record "FID-AUZ-001" [ "Status", "done"; "Depends on", "FID-AUZ-009" ]).Replace("- Negative test: test\n", "")

    let findings = structuralFindings (parse text)
    Assert.Contains(MissingField("FID-AUZ-001", "Negative test"), findings)
    Assert.Contains(UnknownValue("FID-AUZ-001", "Status", "done"), findings)
    Assert.Contains(UnknownDependency("FID-AUZ-001", "FID-AUZ-009"), findings)

[<Fact>]
let ``a statement without an RFC 2119 keyword is reported`` () =
    let text = (record "FID-AUZ-001" []).Replace("MUST do", "does")
    Assert.Contains(NotNormative "FID-AUZ-001", structuralFindings (parse text))

[<Fact>]
let ``a requirement claiming verification without a test is reported`` () =
    let text = record "FID-AUZ-001" [ "Status", "implemented-verified"; "Levels achieved", "unit" ]
    Assert.Contains(VerifiedWithoutTest "FID-AUZ-001", statusFindings Set.empty Set.empty (parse text))

[<Fact>]
let ``a simulated test does not verify a requirement that needs the live provider`` () =
    let text =
        record "FID-AUZ-001" [ "Status", "implemented-verified"; "Levels achieved", "unit, simulated-provider"; "Levels required", "simulated-provider, live-provider" ]

    Assert.Contains(VerifiedBelowRequiredLevel("FID-AUZ-001", "live-provider"), statusFindings (set [ "FID-AUZ-001" ]) Set.empty (parse text))

[<Fact>]
let ``a requirement a test claims cannot be declared missing`` () =
    let text = record "FID-AUZ-001" [ "Status", "missing" ]
    Assert.Contains(ClaimedByTestButMissing("FID-AUZ-001", "missing"), statusFindings (set [ "FID-AUZ-001" ]) (set [ "FID-AUZ-001" ]) (parse text))

[<Fact>]
let ``a requirement that is not verified must be planned by open work`` () =
    let text = record "FID-AUZ-001" [ "Status", "implemented-unverified"; "Levels achieved", "document" ]
    Assert.Contains(OpenButUnplanned("FID-AUZ-001", "implemented-unverified"), statusFindings Set.empty Set.empty (parse text))
    Assert.DoesNotContain(OpenButUnplanned("FID-AUZ-001", "implemented-unverified"), statusFindings Set.empty (set [ "FID-AUZ-001" ]) (parse text))

[<Fact>]
let ``a P0 security requirement needs an adversarial test`` () =
    let text = record "FID-AUZ-001" [ "Negative test", "n/a: nothing to attack" ]
    Assert.Contains(NoAdversarialTest "FID-AUZ-001", statusFindings Set.empty (set [ "FID-AUZ-001" ]) (parse text))

[<Fact>]
let ``the matrix lists every requirement and counts every status`` () =
    let text = record "FID-AUZ-001" [] + "\n\n" + record "FID-IDN-001" [ "Priority", "P1" ]
    let matrix = renderMatrix (parse text)
    Assert.Contains("| FID-AUZ-001 | P0 |", matrix)
    Assert.Contains("| FID-IDN-001 | P1 |", matrix)
    Assert.Contains("| **Total** | 0 | 0 | 2 | 0 | 0 | 2 |", matrix)
