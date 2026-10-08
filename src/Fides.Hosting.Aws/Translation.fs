namespace Fides.Hosting.Aws

open System
open System.Collections.Generic
open System.Text
open Amazon.Lambda.APIGatewayEvents
open Fides

/// Translates API Gateway HTTP API (payload format 2.0) events to the
/// host-neutral request and back. Nothing else (FID-HOST-001).
module Translation =
    /// The path the exchange routes on. A named stage prefixes the raw path
    /// with `/{stage}`; the default stage does not.
    let path (rawPath: string) (stage: string) =
        let prefix = "/" + stage

        if not (String.IsNullOrEmpty stage) && stage <> "$default" && rawPath.StartsWith(prefix + "/", StringComparison.Ordinal) then
            rawPath.Substring prefix.Length
        else
            rawPath

    let private body (request: APIGatewayHttpApiV2ProxyRequest) =
        match request.Body with
        | null -> ""
        | text when request.IsBase64Encoded ->
            try
                Encoding.UTF8.GetString(Convert.FromBase64String text)
            with :? FormatException ->
                // Undecodable: hand the exchange text it will refuse as malformed.
                "\u0000"
        | text -> text

    let toHostRequest (request: APIGatewayHttpApiV2ProxyRequest) : Service.HostRequest =
        let context = request.RequestContext

        let httpMethod, stage =
            match context with
            | null -> "", ""
            | context ->
                (match context.Http with
                 | null -> ""
                 | http -> string http.Method),
                string context.Stage

        let headers =
            match request.Headers with
            | null -> []
            | headers -> [ for KeyValue(name, value) in headers -> name, string value ]

        { Method = httpMethod
          Path = path (string request.RawPath) stage
          Headers = headers
          Body = body request }

    let toLambdaResponse (response: Service.HostResponse) =
        let headers = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        for name, value in response.Headers do
            headers[name] <- value

        APIGatewayHttpApiV2ProxyResponse(StatusCode = response.Status, Headers = headers, Body = response.Body, IsBase64Encoded = false)
