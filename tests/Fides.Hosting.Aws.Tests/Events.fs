/// API Gateway HTTP API (payload format 2.0) events, written as API Gateway
/// sends them and read with the Lambda serializer the function uses.
module Fides.Hosting.Aws.Tests.Events

open System.IO
open System.Text
open Amazon.Lambda.APIGatewayEvents
open Amazon.Lambda.Serialization.SystemTextJson
open Fides.JsonWrite

let private serializer = DefaultLambdaJsonSerializer()

/// The raw event JSON.
let json (httpMethod: string) (rawPath: string) (stage: string) (headers: (string * string) list) (body: string option) (base64: bool) =
    render (
        objectOf
            [ "version", Some(String "2.0")
              "routeKey", Some(String $"{httpMethod} {rawPath}")
              "rawPath", Some(String rawPath)
              "rawQueryString", Some(String "")
              "headers", Some(Object [ for k, v in headers -> k, String v ])
              "requestContext",
              Some(
                  Object
                      [ "accountId", String "000000000000"
                        "apiId", String "fidesapi"
                        "domainName", String "auth.example.test"
                        "http",
                        Object
                            [ "method", String httpMethod
                              "path", String rawPath
                              "protocol", String "HTTP/1.1"
                              "sourceIp", String "192.0.2.1"
                              "userAgent", String "test" ]
                        "requestId", String "req-1"
                        "routeKey", String $"{httpMethod} {rawPath}"
                        "stage", String stage
                        "time", String "08/Oct/2026:09:00:00 +0000"
                        "timeEpoch", Number 1791450000000L ]
              )
              "body", body |> Option.map String
              "isBase64Encoded", Some(Bool base64) ]
    )

let read (eventJson: string) =
    use stream = new MemoryStream(Encoding.UTF8.GetBytes eventJson)
    serializer.Deserialize<APIGatewayHttpApiV2ProxyRequest> stream

let event httpMethod rawPath headers body =
    read (json httpMethod rawPath "$default" headers body false)

let write (response: APIGatewayHttpApiV2ProxyResponse) =
    use stream = new MemoryStream()
    serializer.Serialize(response, stream)
    Encoding.UTF8.GetString(stream.ToArray())
