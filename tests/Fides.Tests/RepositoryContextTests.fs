module Fides.Tests.RepositoryContextTests

open Xunit
open Fides.Tests.RepositoryContext

let private repositoryContext () =
    contextFiles |> List.map (fun path -> path, RepositoryFiles.read path)

let private queue () = queueFile, RepositoryFiles.read queueFile

[<Fact>]
[<Trait("Verifies", "FID-CTX-001")>]
[<Trait("Verifies", "FID-CTX-002")>]
[<Trait("Verifies", "FID-CTX-003")>]
let ``every context file states Fides's purpose and none carries scaffold boilerplate`` () =
    Assert.Empty(findings (repositoryContext ()) (queue ()))

[<Fact>]
[<Trait("Verifies", "FID-CTX-003")>]
let ``a charter reverted to the scaffold boilerplate fails the gate`` () =
    let reverted =
        "# Fides project charter\n\n## Purpose\n\nDefine the practical problem this project will solve.\n\n## Intended users\n\nNot yet established.\n"

    let result = findings [ "PROJECT-CHARTER.md", reverted ] ("queue.md", "")

    Assert.Contains(MissingPurpose "PROJECT-CHARTER.md", result)
    Assert.Contains(Boilerplate("PROJECT-CHARTER.md", "Not yet established"), result)
    Assert.Contains(Boilerplate("PROJECT-CHARTER.md", "Define the practical problem this project will solve"), result)

[<Fact>]
[<Trait("Verifies", "FID-CTX-003")>]
let ``a context file that keeps the purpose but reintroduces boilerplate fails the gate`` () =
    let drifted = purposeStatement + "\n\nThe first communication problem is not yet chosen.\n"
    Assert.Equal<Finding list>([ Boilerplate("context/CURRENT-STATE.md", "communication problem") ], contextFindings ("context/CURRENT-STATE.md", drifted))

[<Fact>]
[<Trait("Verifies", "FID-CTX-003")>]
let ``boilerplate in the work queue fails the gate`` () =
    Assert.Equal<Finding list>([ Boilerplate(".ros/work/queue.md", "greenfield pilot") ], findings [] (".ros/work/queue.md", "| WI-9 | a greenfield pilot task |"))
