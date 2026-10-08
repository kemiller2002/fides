namespace Fides.Hosting.Aws

open System
open System.Threading.Tasks
open Amazon.Lambda.APIGatewayEvents
open Amazon.Lambda.Core
open Fides
open Fides.Hosting

/// The Lambda function: translate, run the exchange, translate back, and log
/// the audit line and nothing else.
module Function =
    /// The environment variable holding the configuration document.
    [<Literal>]
    let ConfigurationVariable = "FIDES_CONFIGURATION"

    let handle (configuration: Configuration) (ports: Ports) (request: APIGatewayHttpApiV2ProxyRequest) (context: ILambdaContext) =
        task {
            let! response, line = Host.handle configuration ports (Translation.toHostRequest request)

            context.Logger.LogInformation line

            return Translation.toLambdaResponse response
        }

    /// The ports a deployed function uses: an HttpClient with a ten-second
    /// timeout, Secrets Manager behind a five-minute cache, and the system clock.
    let livePorts (secrets: SecretReference -> Task<Result<ClientSecret, unit>>) =
        let client = HttpPort.client ()
        let now () = DateTimeOffset.UtcNow

        { Send = HttpPort.send client (TimeSpan.FromSeconds 10.)
          ReadSecret = SecretCache.cached (TimeSpan.FromMinutes 5.) now secrets
          Now = now }
