module Fides.Hosting.Aws.Program

open System
open System.Threading.Tasks
open Amazon.Lambda.APIGatewayEvents
open Amazon.Lambda.Core
open Amazon.Lambda.RuntimeSupport
open Amazon.Lambda.Serialization.SystemTextJson
open Amazon.SecretsManager
open Fides.Hosting

/// Starts the Lambda runtime loop. An invalid configuration stops the function
/// at initialisation with the problems on standard error, so a bad deployment
/// fails visibly instead of answering requests.
[<EntryPoint>]
let main _ =
    match Host.configuration (Environment.GetEnvironmentVariable Function.ConfigurationVariable |> Option.ofObj) with
    | Error problems ->
        eprintfn "Fides configuration is invalid: %s" (String.concat "; " problems)
        1
    | Ok configuration ->
        let ports = Function.livePorts (SecretsManagerPort.live (new AmazonSecretsManagerClient()))

        let handler =
            Func<APIGatewayHttpApiV2ProxyRequest, ILambdaContext, Task<APIGatewayHttpApiV2ProxyResponse>>(fun request context ->
                Function.handle configuration ports request context)

        use bootstrap = LambdaBootstrapBuilder.Create<APIGatewayHttpApiV2ProxyRequest, APIGatewayHttpApiV2ProxyResponse>(handler, DefaultLambdaJsonSerializer()).Build()
        bootstrap.RunAsync().GetAwaiter().GetResult()
        0
