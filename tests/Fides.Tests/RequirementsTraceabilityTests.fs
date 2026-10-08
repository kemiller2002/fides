module Fides.Tests.RequirementsTraceabilityTests

open Xunit
open Fides.Tests.RequirementsTraceability

let private declared () =
    declaredRequirements (RepositoryFiles.read "docs/requirements/FIDES-REQUIREMENTS.md")

let private planned () =
    namedRequirements (openWorkText (RepositoryFiles.read ".ros/work/queue.json"))

let private thisAssembly () =
    verifiedRequirements (System.Reflection.Assembly.GetExecutingAssembly())

/// Every test project's sources under tests/.
let private testSources () =
    System.IO.Directory.EnumerateFiles(System.IO.Path.Combine(RepositoryFiles.root (), "tests"), "*.fs", System.IO.SearchOption.AllDirectories)
    |> Seq.filter (fun path -> not (path.Contains "/obj/" || path.Contains "/bin/"))
    |> Seq.map System.IO.File.ReadAllText

let private verified () = verifiedInSources (testSources ())

[<Fact>]
let ``the requirements document declares requirement IDs`` () = Assert.NotEmpty(declared ())

[<Fact>]
let ``every Fides requirement is planned by an open work item or verified by a test`` () =
    Assert.Empty(unaccounted (declared ()) (planned ()) (verified ()))

[<Fact>]
let ``every verification claim names a declared requirement`` () =
    Assert.Empty(unknownVerifications (declared ()) (verified ()))

[<Fact>]
let ``a requirement that no open work item names is reported`` () =
    let queue = """{"items":[{"status":"captured","title":"covers FID-TB-001..002","description":null}]}"""
    let declared = declaredRequirements "**FID-TB-001** **FID-TB-002** **FID-EXC-001**"
    Assert.Equal<Set<string>>(Set.ofList [ "FID-EXC-001" ], unaccounted declared (namedRequirements (openWorkText queue)) Set.empty)

[<Fact>]
let ``a requirement whose work item completed stays accounted for only while a test verifies it`` () =
    let queue = """{"items":[{"status":"complete","title":"covers FID-TB-001","description":null}]}"""
    let declared = declaredRequirements "**FID-TB-001**"
    let planned = namedRequirements (openWorkText queue)
    Assert.Equal<Set<string>>(Set.ofList [ "FID-TB-001" ], unaccounted declared planned Set.empty)
    Assert.Empty(unaccounted declared planned (Set.ofList [ "FID-TB-001" ]))

[<Fact>]
let ``this test assembly's Verifies traits are discovered`` () =
    Assert.Contains("FID-CTX-003", thisAssembly ())

[<Fact>]
let ``the source scan sees every claim this assembly makes and the adapter projects' claims`` () =
    Assert.Empty(Set.difference (thisAssembly ()) (verified ()))
    Assert.Contains("FID-HOST-003", verified ())
