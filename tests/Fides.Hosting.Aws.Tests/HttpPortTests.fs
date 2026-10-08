module Fides.Hosting.Aws.Tests.HttpPortTests

open Xunit
open Fides
open Fides.Hosting

[<Fact>]
let ``a form request is sent form-encoded with its headers`` () =
    use message =
        HttpPort.message
            { Method = "POST"
              Url = "https://github.com/login/oauth/access_token"
              Headers = [ "Accept", "application/json"; "User-Agent", "Fides" ]
              Body = Form [ "client_id", "a b"; "code", "c&d" ] }

    Assert.Equal("application/x-www-form-urlencoded", string (nonNull (nonNull message.Content).Headers.ContentType).MediaType)
    Assert.Equal("client_id=a+b&code=c%26d", (nonNull message.Content).ReadAsStringAsync().Result)
    Assert.Equal("Fides", string message.Headers.UserAgent)

[<Fact>]
let ``a JSON request is sent as UTF-8 JSON`` () =
    use message =
        HttpPort.message
            { Method = "DELETE"
              Url = "https://api.github.com/applications/x/token"
              Headers = [ "Authorization", "Basic eDp5" ]
              Body = Json """{"access_token":"t"}""" }

    Assert.Equal("application/json", string (nonNull (nonNull message.Content).Headers.ContentType).MediaType)
    Assert.Equal("Basic eDp5", string message.Headers.Authorization)

[<Fact>]
let ``the provider client does not follow redirects and bounds responses`` () =
    use client = HttpPort.client ()
    Assert.Equal(1048576L, client.MaxResponseContentBufferSize)
