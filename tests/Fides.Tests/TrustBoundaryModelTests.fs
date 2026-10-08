/// FID-TB-001..006 are "MUST define" requirements: the definitions are the
/// trust-boundary model and decision record. These tests keep each definition
/// present; the behaviour each rule implies is verified by the exchange,
/// adapter and client tests that cite the same requirement.
module Fides.Tests.TrustBoundaryModelTests

open System.Text.RegularExpressions
open Xunit

let private model () = RepositoryFiles.read "docs/architecture/TRUST-BOUNDARIES.md"

let private decision () =
    RepositoryFiles.read "research/decisions/DF-FIDES-2026-0007--what-github-proves-and-what-fides-owns.md"

/// Level-2 headings of a Markdown document.
let headings (markdown: string) =
    Regex.Matches(markdown, @"^## (.+)$", RegexOptions.Multiline) |> Seq.map _.Groups[1].Value |> List.ofSeq

/// Whether some heading defines `requirement`, as "Title (FID-TB-00N)".
let definesRequirement (requirement: string) (markdown: string) =
    headings markdown |> List.exists _.Contains($"({requirement})")

[<Theory>]
[<InlineData("FID-TB-001")>]
[<InlineData("FID-TB-002")>]
[<InlineData("FID-TB-003")>]
[<InlineData("FID-TB-004")>]
[<InlineData("FID-TB-005")>]
[<InlineData("FID-TB-006")>]
[<Trait("Verifies", "FID-TB-001")>]
[<Trait("Verifies", "FID-TB-002")>]
[<Trait("Verifies", "FID-TB-003")>]
[<Trait("Verifies", "FID-TB-004")>]
[<Trait("Verifies", "FID-TB-005")>]
let ``the trust-boundary model has a section defining each FID-TB requirement`` (requirement: string) =
    Assert.True(definesRequirement requirement (model ()), $"no section of TRUST-BOUNDARIES.md defines {requirement}")

[<Fact>]
let ``a model missing a section is detected`` () =
    Assert.False(definesRequirement "FID-TB-004" "## 1. Users (FID-TB-001)\n\n## 3. Ownership (FID-TB-002)\n")

[<Fact>]
let ``the trust-boundary model names every material that crosses a boundary`` () =
    let text = model ()

    for material in [ "Client secret"; "`state`"; "PKCE verifier"; "Authorization code"; "Access token"; "Refresh token" ] do
        Assert.Contains(material, text)

[<Fact>]
[<Trait("Verifies", "FID-TB-006")>]
let ``the GitHub-proves decision is accepted and states both sides`` () =
    let text = decision ()
    Assert.Contains("status: accepted", text)
    Assert.Contains("**GitHub proves**", text)
    Assert.Contains("**GitHub does not prove**, and Fides must own", text)

    for owned in [ "Client registration"; "Redirect policy"; "Request binding"; "Session states"; "Revocation propagation"; "Secret custody" ] do
        Assert.Contains(owned, text)
