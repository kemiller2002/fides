module Fides.Tests.PkceTests

open System
open Xunit
open Fides

[<Fact>]
[<Trait("Verifies", "FID-EXC-001")>]
let ``the S256 challenge matches RFC 7636 appendix B`` () =
    let verifier = Pkce.parseVerifier "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"

    match verifier with
    | Ok v -> Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", Pkce.challenge v)
    | Error() -> failwith "RFC vector rejected"

[<Theory>]
[<InlineData(42, false)>]
[<InlineData(43, true)>]
[<InlineData(128, true)>]
[<InlineData(129, false)>]
[<Trait("Verifies", "FID-EXC-001")>]
let ``a verifier is 43 to 128 characters`` (length: int, valid: bool) =
    Assert.Equal(valid, Result.isOk (Pkce.parseVerifier (String('a', length))))

[<Theory>]
[<InlineData(' ')>]
[<InlineData('+')>]
[<InlineData('/')>]
[<InlineData('=')>]
[<InlineData('%')>]
[<InlineData('é')>]
[<Trait("Verifies", "FID-EXC-001")>]
let ``a verifier holds only unreserved characters`` (c: char) =
    Assert.True(Result.isError (Pkce.parseVerifier (String('a', 42) + string c)))

[<Fact>]
let ``32 random bytes make a 43-character verifier`` () =
    match Pkce.verifierFrom (Array.init 32 byte) with
    | Ok v -> Assert.Equal(43, (Secret.reveal v).Length)
    | Error() -> failwith "rejected"

    Assert.True(Result.isError (Pkce.verifierFrom (Array.zeroCreate 31)))
