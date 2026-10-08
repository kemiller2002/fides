/// Every Fides requirement is accounted for: either an open work item in the
/// Praxis queue (captured, ready, active or blocked) still plans it, naming it
/// directly (`FID-EXC-003`) or inside a range of the same family
/// (`FID-TB-001..006`), or a test verifies it, by carrying
/// `[<Trait("Verifies", "FID-...")>]`. A requirement that neither plans nor
/// verifies would silently fall out of the product.
module Fides.Tests.RequirementsTraceability

open System
open System.Reflection
open System.Text.Json
open System.Text.RegularExpressions

/// Requirement IDs declared in the requirements document (`**FID-XXX-NNN**`).
let declaredRequirements (document: string) =
    Regex.Matches(document, @"\*\*(FID-[A-Z]+-\d{3})\*\*")
    |> Seq.map _.Groups[1].Value
    |> Set.ofSeq

let private isOpen (item: JsonElement) =
    match item.GetProperty("status").GetString() with
    | "complete"
    | "abandoned" -> false
    | _ -> true

let private text (item: JsonElement) (name: string) =
    match item.TryGetProperty name with
    | true, value when value.ValueKind = JsonValueKind.String -> string (value.GetString())
    | _ -> ""

/// Title and description of every open work item in a Praxis queue.json.
let openWorkText (queueJson: string) =
    use queue = JsonDocument.Parse queueJson

    queue.RootElement.GetProperty("items").EnumerateArray()
    |> Seq.filter isOpen
    |> Seq.map (fun item -> text item "title" + "\n" + text item "description")
    |> String.concat "\n"

/// Requirement IDs named in `work`, directly or through `FAMILY-NNN..MMM` ranges.
let namedRequirements (work: string) =
    let direct = Regex.Matches(work, @"FID-[A-Z]+-\d{3}") |> Seq.map _.Value

    let ranges =
        Regex.Matches(work, @"(FID-[A-Z]+)-(\d{3})\.\.(\d{3})")
        |> Seq.collect (fun m ->
            let family, low, high = m.Groups[1].Value, int m.Groups[2].Value, int m.Groups[3].Value
            seq { for n in low..high -> sprintf "%s-%03d" family n })

    Seq.append direct ranges |> Set.ofSeq

/// The trait name a test uses to declare the requirement it verifies.
[<Literal>]
let VerifiesTrait = "Verifies"

/// Requirement IDs that test methods in `assembly` declare they verify.
let verifiedRequirements (assembly: Assembly) =
    assembly.GetTypes()
    |> Seq.collect (fun t -> t.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.Instance))
    |> Seq.collect (fun m -> m.CustomAttributes)
    |> Seq.filter (fun a -> a.AttributeType.FullName = "Xunit.TraitAttribute")
    |> Seq.choose (fun a ->
        match [ for arg in a.ConstructorArguments -> string arg.Value ] with
        | [ VerifiesTrait; requirement ] -> Some requirement
        | _ -> None)
    |> Set.ofSeq

/// Declared requirements that no open work item plans and no test verifies.
let unaccounted (declared: Set<string>) (planned: Set<string>) (verified: Set<string>) =
    Set.difference declared (Set.union planned verified)

/// Verification claims for requirements the document does not declare.
let unknownVerifications (declared: Set<string>) (verified: Set<string>) = Set.difference verified declared
