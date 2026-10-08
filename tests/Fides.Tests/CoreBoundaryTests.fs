/// The core is pure and cloud-agnostic: it references no cloud SDK and no
/// networking or I/O library (FID-EXC-002, FID-HOST-001).
module Fides.Tests.CoreBoundaryTests

open Xunit

let private references () =
    typeof<Fides.ProviderId>.Assembly.GetReferencedAssemblies()
    |> Array.choose (fun a -> a.Name |> Option.ofObj)
    |> Set.ofArray

[<Fact>]
[<Trait("Verifies", "FID-EXC-002")>]
[<Trait("Verifies", "FID-HOST-001")>]
let ``the core references no cloud SDK, HTTP client or file system`` () =
    let forbidden =
        references ()
        |> Set.filter (fun name ->
            [ "AWSSDK"; "Amazon"; "Azure"; "Microsoft.Azure"; "System.Net"; "System.IO.FileSystem"; "Microsoft.Extensions" ]
            |> List.exists name.StartsWith)

    Assert.Empty forbidden

[<Fact>]
let ``the core's references are the base library, FSharp.Core and System.Text.Json`` () =
    let allowed (name: string) =
        name = "FSharp.Core"
        || name = "netstandard"
        || name.StartsWith "System.Runtime"
        || name.StartsWith "System.Text"
        || name.StartsWith "System.Security.Cryptography"
        || name.StartsWith "System.Collections"
        || name = "System.Memory"
        || name = "System.Linq"
        || name = "System.Private.CoreLib"

    Assert.Empty(references () |> Set.filter (allowed >> not))
