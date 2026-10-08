module Fides.Hosting.Aws.Tests.SecretsTests

open System
open System.Threading.Tasks
open Amazon.SecretsManager.Model
open Xunit
open Fides
open Fides.Hosting
open Fides.Hosting.Aws

let private reference = SecretReference "arn:aws:secretsmanager:us-east-1:000000000000:secret:fides-test"

let private answering (secretString: string | null) =
    fun (_: GetSecretValueRequest) -> Task.FromResult(GetSecretValueResponse(SecretString = secretString))

let private read get =
    (SecretsManagerPort.reader get reference).GetAwaiter().GetResult()

[<Fact>]
[<Trait("Verifies", "FID-HOST-002")>]
let ``the client secret is the secret string, trimmed`` () =
    match read (answering "  the-secret\n") with
    | Ok secret -> Assert.Equal("the-secret", Secret.reveal secret)
    | Error() -> failwith "expected the secret"

[<Fact>]
let ``the request names the configured secret`` () =
    let mutable asked = ""

    let get (request: GetSecretValueRequest) =
        asked <- request.SecretId
        Task.FromResult(GetSecretValueResponse(SecretString = "s"))

    read get |> ignore
    Assert.Equal("arn:aws:secretsmanager:us-east-1:000000000000:secret:fides-test", asked)

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
let ``an empty secret is unavailable`` (value: string) = Assert.Equal(Error(), read (answering value))

[<Fact>]
let ``a binary-only secret is unavailable`` () = Assert.Equal(Error(), read (answering null))

[<Fact>]
let ``a Secrets Manager failure is unavailable and does not throw`` () =
    let failing (_: GetSecretValueRequest) : Task<GetSecretValueResponse> =
        Task.FromException<GetSecretValueResponse>(ResourceNotFoundException "Secrets Manager can't find the specified secret.")

    Assert.Equal(Error(), read failing)

[<Fact>]
let ``the secret cache serves a warm secret until it expires`` () =
    let mutable reads = 0
    let mutable now = DateTimeOffset.UnixEpoch

    let source _ =
        reads <- reads + 1
        Task.FromResult(Ok(Secret.create<SecretKind.ClientSecret> $"secret-{reads}"))

    let cached = SecretCache.cached (TimeSpan.FromMinutes 5.) (fun () -> now) source
    let get () = (cached reference).GetAwaiter().GetResult()
    let first = get ()
    now <- now.AddMinutes 4.
    Assert.Equal(first, get ())
    Assert.Equal(1, reads)
    now <- now.AddMinutes 2.
    Assert.NotEqual(first, get ())
    Assert.Equal(2, reads)

[<Fact>]
let ``the secret cache does not keep a failure`` () =
    let mutable reads = 0

    let source _ =
        reads <- reads + 1
        Task.FromResult(if reads = 1 then Error() else Ok(Secret.create<SecretKind.ClientSecret> "s"))

    let cached = SecretCache.cached (TimeSpan.FromMinutes 5.) (fun () -> DateTimeOffset.UnixEpoch) source
    Assert.Equal(Error(), (cached reference).GetAwaiter().GetResult())
    Assert.True(Result.isOk ((cached reference).GetAwaiter().GetResult()))
