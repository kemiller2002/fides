module Fides.Tests.LibraryTests

open Xunit

[<Fact>]
let ``scaffold builds and links the library`` () =
    Assert.True Fides.Library.scaffoldReady
