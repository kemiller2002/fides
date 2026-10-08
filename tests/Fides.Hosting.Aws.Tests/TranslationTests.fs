module Fides.Hosting.Aws.Tests.TranslationTests

open System
open System.Text
open Xunit
open Fides
open Fides.Hosting.Aws

[<Fact>]
[<Trait("Verifies", "FID-HOST-001")>]
[<Trait("Verifies", "FID-HOST-002")>]
let ``an API Gateway event becomes the host-neutral request`` () =
    let request =
        Events.event "POST" "/v1/token" [ "content-type", "application/json"; "origin", "https://chrona.example" ] (Some """{"a":1}""")
        |> Translation.toHostRequest

    Assert.Equal("POST", request.Method)
    Assert.Equal("/v1/token", request.Path)
    Assert.Equal("""{"a":1}""", request.Body)
    Assert.Equal(Some "https://chrona.example", Service.header "Origin" request)
    Assert.Equal(Some "application/json", Service.header "Content-Type" request)

[<Fact>]
let ``a named stage's path prefix is removed`` () =
    let request =
        Events.read (Events.json "POST" "/live/v1/token" "live" [] None false) |> Translation.toHostRequest

    Assert.Equal("/v1/token", request.Path)

[<Theory>]
[<InlineData("/v1/token", "$default", "/v1/token")>]
[<InlineData("/live/v1/token", "live", "/v1/token")>]
[<InlineData("/live", "live", "/live")>]
[<InlineData("/lively/v1/token", "live", "/lively/v1/token")>]
let ``only an exact stage segment is removed`` (raw: string, stage: string, expected: string) =
    Assert.Equal(expected, Translation.path raw stage)

[<Fact>]
let ``a base64 body is decoded`` () =
    let body = Convert.ToBase64String(Encoding.UTF8.GetBytes """{"application":"x"}""")
    let request = Events.read (Events.json "POST" "/v1/token" "$default" [] (Some body) true) |> Translation.toHostRequest
    Assert.Equal("""{"application":"x"}""", request.Body)

[<Fact>]
let ``an undecodable base64 body cannot be mistaken for a request`` () =
    let request = Events.read (Events.json "POST" "/v1/token" "$default" [] (Some "%%%") true) |> Translation.toHostRequest
    Assert.Equal(None, Protocol.decodeTokenRequest request.Body)

[<Fact>]
let ``an event without body or headers is an empty request`` () =
    let request = Events.read (Events.json "OPTIONS" "/v1/token" "$default" [] None false) |> Translation.toHostRequest
    Assert.Equal("", request.Body)
    Assert.Equal("OPTIONS", request.Method)

[<Fact>]
let ``the response keeps status, headers and body`` () =
    let response =
        Translation.toLambdaResponse
            { Status = 403
              Headers = [ "Cache-Control", "no-store"; "Content-Type", "application/json" ]
              Body = """{"error":"origin_not_allowed"}""" }

    Assert.Equal(403, response.StatusCode)
    Assert.Equal("no-store", response.Headers["cache-control"])
    Assert.False response.IsBase64Encoded
    Assert.Contains("\"statusCode\":403", Events.write response)
