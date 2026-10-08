namespace Fides.Hosting.Aws

open System.Threading.Tasks
open Amazon.SecretsManager
open Amazon.SecretsManager.Model
open Fides

/// Reads the GitHub App client secret from AWS Secrets Manager. The secret is
/// stored as a plain SecretString holding only the client secret.
module SecretsManagerPort =
    /// A reader over any `GetSecretValue` call, so the mapping is testable
    /// without AWS. It never throws: every failure is `Error()`, and the
    /// exception, which can name the secret, is not passed on.
    let reader (getSecretValue: GetSecretValueRequest -> Task<GetSecretValueResponse>) (SecretReference reference) =
        task {
            try
                let! response = getSecretValue (GetSecretValueRequest(SecretId = reference))

                match response.SecretString with
                | null -> return Error()
                | value when value.Trim().Length = 0 -> return Error()
                | value -> return Ok(Secret.create<SecretKind.ClientSecret> (value.Trim()))
            with _ ->
                return Error()
        }

    /// A reader backed by the Lambda's own Secrets Manager client.
    let live (client: AmazonSecretsManagerClient) =
        reader (fun request -> client.GetSecretValueAsync request)
