/// The release input names exactly what the solution packs, so a package
/// cannot ship undeclared or be declared and never built (FID-CLI-004).
module Fides.Tests.ReleaseTests

open System.IO
open System.Text.RegularExpressions
open Xunit

let private packageIds () =
    Directory.EnumerateFiles(Path.Combine(RepositoryFiles.root (), "src"), "*.fsproj", SearchOption.AllDirectories)
    |> Seq.map File.ReadAllText
    |> Seq.filter (fun project -> project.Contains "<IsPackable>true</IsPackable>")
    |> Seq.map (fun project -> Regex.Match(project, "<PackageId>(.+?)</PackageId>").Groups[1].Value)
    |> Set.ofSeq

let private declared () =
    let input = RepositoryFiles.read "release/echelon.release-input.json"

    Regex.Matches(input, "\"name\": \"(.+?)\\.\\{version\\}\\.nupkg\"")
    |> Seq.map _.Groups[1].Value
    |> Set.ofSeq

[<Fact>]
[<Trait("Verifies", "FID-CLI-004")>]
let ``the release declares every packable project and nothing else`` () =
    Assert.Equal<Set<string>>(Set.ofList [ "EchelonFoundry.Fides"; "EchelonFoundry.Fides.Arca"; "EchelonFoundry.Fides.Client"; "EchelonFoundry.Fides.Hosting" ], packageIds ())
    Assert.Equal<Set<string>>(packageIds (), declared ())

[<Fact>]
[<Trait("Verifies", "FID-CLI-004")>]
let ``the release ships the AWS Lambda package and publishes through attested GitHub releases`` () =
    let input = RepositoryFiles.read "release/echelon.release-input.json"
    Assert.Contains("\"fides-exchange-linux-arm64.zip\"", input)
    Assert.Contains("\"mechanism\": \"github-release\"", input)
    let workflow = RepositoryFiles.read ".github/workflows/release.yml"
    Assert.Contains("actions/attest-build-provenance@", workflow)
    Assert.Contains("gh attestation verify", workflow)

[<Fact>]
let ``the version is a release version`` () =
    let props = RepositoryFiles.read "Directory.Build.props"
    Assert.Matches(@"<Version>\d+\.\d+\.\d+</Version>", props)
