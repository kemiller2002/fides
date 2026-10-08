/// The CloudFormation template deploys one environment per stack with its own
/// GitHub App, hard-codes no account, region, domain or secret, and grants the
/// function only what it needs (FID-HOST-002, FID-HOST-003, DF-FIDES-2026-0005).
module Fides.Hosting.Aws.Tests.TemplateTests

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open Xunit
open Fides

let rec private root (directory: DirectoryInfo | null) =
    match directory with
    | Null -> failwith "repository root not found"
    | NonNull d when File.Exists(Path.Combine(d.FullName, "Fides.slnx")) -> d.FullName
    | NonNull d -> root d.Parent

let private path = Path.Combine(root (DirectoryInfo AppContext.BaseDirectory), "infrastructure", "aws", "fides-exchange.template.json")
let private text = File.ReadAllText path
let private template = JsonDocument.Parse(text).RootElement
let private resources = template.GetProperty "Resources"

let private ofType (kind: string) =
    resources.EnumerateObject()
    |> Seq.filter (fun r -> string (r.Value.GetProperty("Type").GetString()) = kind)
    |> Seq.map _.Value
    |> List.ofSeq

let private single kind =
    match ofType kind with
    | [ one ] -> one.GetProperty "Properties"
    | many -> failwith $"expected one {kind}, found {many.Length}"

[<Fact>]
[<Trait("Verifies", "FID-HOST-003")>]
let ``one stack serves one named environment with its own GitHub App`` () =
    let parameters = template.GetProperty "Parameters"
    let environments = parameters.GetProperty("EnvironmentName").GetProperty("AllowedValues").EnumerateArray() |> Seq.map (fun e -> string (e.GetString())) |> List.ofSeq
    Assert.Equal<string list>([ "test"; "staging"; "production" ], environments)

    for name in [ "DomainName"; "CertificateArn"; "GitHubClientId"; "ClientSecretArn"; "ApplicationsJson"; "CodeBucket"; "CodeKey" ] do
        let parameter = parameters.GetProperty name
        Assert.False(parameter.TryGetProperty("Default") |> fst, $"{name} must be supplied per deployment, not defaulted")

[<Fact>]
[<Trait("Verifies", "FID-HOST-002")>]
let ``the template hard-codes no account, region, ARN or secret`` () =
    Assert.DoesNotMatch(@"\b\d{12}\b", text)
    Assert.DoesNotMatch(@"\b(us|eu|ap|sa|ca|me|af|il|mx)-[a-z]+-\d\b", text)
    Assert.DoesNotMatch(@"arn:aws:[a-z]", text)

    for marker in [ "ghu_"; "ghr_"; "ghs_"; "BEGIN"; "client_secret"; "PRIVATE KEY" ] do
        Assert.DoesNotContain(marker, text)

[<Fact>]
[<Trait("Verifies", "FID-HOST-002")>]
let ``the function may read exactly the client secret and write its own logs, nothing else`` () =
    let role = single "AWS::IAM::Role"

    /// (sid, action, resource) for every policy statement, as plain text.
    let statements =
        [ for policy in role.GetProperty("Policies").EnumerateArray() do
              for statement in policy.GetProperty("PolicyDocument").GetProperty("Statement").EnumerateArray() ->
                  string (statement.GetProperty("Sid").GetString()),
                  statement.GetProperty("Action").GetRawText(),
                  statement.GetProperty("Resource").GetRawText() ]

    Assert.Equal(2, statements.Length)
    let _, action, resource = statements |> List.find (fun (sid, _, _) -> sid = "ReadClientSecretOnly")
    Assert.Equal("\"secretsmanager:GetSecretValue\"", action)
    Assert.Equal("""{"Ref":"ClientSecretArn"}""", Regex.Replace(resource, @"\s", ""))
    Assert.DoesNotContain("\"Resource\": \"*\"", text)
    Assert.DoesNotContain("\"Action\": \"*\"", text)

[<Fact>]
[<Trait("Verifies", "FID-HOST-002")>]
let ``the function is the provided.al2023 bootstrap on arm64, configured with a secret reference`` () =
    let fn = single "AWS::Lambda::Function"
    Assert.Equal("provided.al2023", fn.GetProperty("Runtime").GetString() |> string)
    Assert.Equal("bootstrap", fn.GetProperty("Handler").GetString() |> string)
    Assert.Equal("arm64", (fn.GetProperty "Architectures").[0].GetString() |> string)

    let configuration =
        fn.GetProperty("Environment").GetProperty("Variables").GetProperty("FIDES_CONFIGURATION").GetProperty("Fn::Sub").GetString() |> string

    Assert.Contains("\"clientSecret\":\"${ClientSecretArn}\"", configuration)
    Assert.Contains("\"applications\":${ApplicationsJson}", configuration)

    let rendered =
        configuration
            .Replace("${GitHubClientId}", "Iv23fixture")
            .Replace("${ClientSecretArn}", "arn:aws:secretsmanager:us-east-1:000000000000:secret:x")
            .Replace("${ApplicationsJson}", """[{"id":"chrona-test","provider":"github","origins":["https://chrona.example"],"redirectUris":["https://chrona.example/auth/callback"]}]""")

    Assert.True(Result.isOk (Fides.Hosting.Host.configuration (Some rendered)), "the rendered configuration must parse")

[<Fact>]
[<Trait("Verifies", "FID-HOST-005")>]
let ``the HTTP API routes exactly the protocol's paths and leaves CORS to the function`` () =
    let api = single "AWS::ApiGatewayV2::Api"
    Assert.False(api.TryGetProperty("CorsConfiguration") |> fst, "CORS must be answered by the function's exact allow-list")
    Assert.True(api.GetProperty("DisableExecuteApiEndpoint").GetBoolean())

    let routes =
        ofType "AWS::ApiGatewayV2::Route"
        |> List.map (fun r -> string (r.GetProperty("Properties").GetProperty("RouteKey").GetString()))
        |> Set.ofList

    let expected =
        Set.ofList [ for p in [ Protocol.TokenPath; Protocol.RefreshPath; Protocol.RevokePath ] do
                         for m in [ "POST"; "OPTIONS" ] -> $"{m} {p}" ]

    Assert.Equal<Set<string>>(expected, routes)

[<Fact>]
let ``the API uses payload format 2.0, TLS 1.2, throttling and bounded log retention`` () =
    Assert.Equal("2.0", (single "AWS::ApiGatewayV2::Integration").GetProperty("PayloadFormatVersion").GetString() |> string)
    let domain = ((single "AWS::ApiGatewayV2::DomainName").GetProperty "DomainNameConfigurations").[0]
    Assert.Equal("TLS_1_2", domain.GetProperty("SecurityPolicy").GetString() |> string)
    Assert.Equal("REGIONAL", domain.GetProperty("EndpointType").GetString() |> string)
    let stage = single "AWS::ApiGatewayV2::Stage"
    Assert.True(stage.GetProperty("DefaultRouteSettings").TryGetProperty("ThrottlingRateLimit") |> fst)

    for group in ofType "AWS::Logs::LogGroup" do
        Assert.True(group.GetProperty("Properties").TryGetProperty("RetentionInDays") |> fst)

    Assert.DoesNotContain("sourceIp", text)

[<Fact>]
[<Trait("Verifies", "FID-HOST-003")>]
let ``the hosting guide documents every template parameter and the GitHub App's required settings`` () =
    let guide = File.ReadAllText(Path.Combine(root (DirectoryInfo AppContext.BaseDirectory), "docs", "hosting", "AWS.md"))

    for parameter in (template.GetProperty "Parameters").EnumerateObject() do
        Assert.Contains($"`{parameter.Name}`", guide)

    for setting in [ "Expire user authorization tokens"; "Contents: Read and write"; "Webhook"; "Callback URL"; "Client ID" ] do
        Assert.Contains(setting, guide)
