module Fides.Tests.SecretsTests

open Xunit
open Fides

[<Fact>]
[<Trait("Verifies", "FID-EXC-005")>]
let ``a secret prints as redacted however it is formatted`` () =
    let token: AccessToken = Secret.create "ghu_supersecret"
    Assert.Equal("<redacted>", string token)
    Assert.Equal("<redacted>", $"{token}")
    Assert.Equal("<redacted>", sprintf "%A" token)
    Assert.Equal("<redacted>", sprintf "%O" token)

[<Fact>]
[<Trait("Verifies", "FID-EXC-005")>]
let ``a record holding secrets prints without them`` () =
    let grant =
        { AccessToken = Secret.create "ghu_supersecret"
          AccessTokenExpiresAt = System.DateTimeOffset.UnixEpoch
          RefreshToken = Secret.create "ghr_supersecret"
          RefreshTokenExpiresAt = System.DateTimeOffset.UnixEpoch }

    let text = sprintf "%A" grant
    Assert.DoesNotContain("supersecret", text)

[<Fact>]
let ``secrets of the same kind compare by value and reveal explicitly`` () =
    let a: RefreshToken = Secret.create "x"
    Assert.Equal(a, Secret.create "x")
    Assert.NotEqual(a, Secret.create "y")
    Assert.Equal("x", Secret.reveal a)
