/// Reading repository files from the test run, located from the test assembly.
module Fides.Tests.RepositoryFiles

open System
open System.IO

/// The repository root: the nearest directory above `start` that holds Fides.slnx.
let rec repositoryRoot (start: DirectoryInfo | null) =
    match start with
    | Null -> None
    | NonNull directory when File.Exists(Path.Combine(directory.FullName, "Fides.slnx")) -> Some directory.FullName
    | NonNull directory -> repositoryRoot directory.Parent

/// The repository root of this test run.
let root () =
    match repositoryRoot (DirectoryInfo AppContext.BaseDirectory) with
    | Some root -> root
    | None -> failwith "repository root (Fides.slnx) not found above the test assembly"

/// The text of a repository file, by repository-relative path.
let read (relative: string) = File.ReadAllText(Path.Combine(root (), relative))
