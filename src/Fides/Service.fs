namespace Fides

open System
open System.Text
open Fides.Protocol

/// The exchange as an HTTP service, still pure: a host-neutral request in,
/// an effect producing a host-neutral response out. A hosting adapter only
/// translates its platform's request and response shapes to these and runs
/// the effects (FID-HOST-001).
module Service =
    /// An HTTP request as any host receives it.
    [<StructuredFormatDisplay("{Display}")>]
    type HostRequest =
        { Method: string
          Path: string
          Headers: (string * string) list
          Body: string }

        member this.Display = $"{this.Method} {this.Path}"
        override this.ToString() = this.Display

    /// An HTTP response for the host to send. The body can carry tokens.
    [<StructuredFormatDisplay("{Display}")>]
    type HostResponse =
        { Status: int
          Headers: (string * string) list
          Body: string }

        member this.Display = $"HTTP {this.Status}"
        override this.ToString() = this.Display

    /// What the host may log about a request. It holds no request or response
    /// content: the operation, the application only if it is registered, and
    /// the outcome code (FID-EXC-005).
    type AuditRecord =
        { Operation: string
          Application: string option
          Outcome: string }

    let header (name: string) (request: HostRequest) =
        request.Headers
        |> List.tryFind (fun (k, _) -> String.Equals(k, name, StringComparison.OrdinalIgnoreCase))
        |> Option.map snd

    let private allOrigins (configuration: Configuration) =
        configuration.Applications |> Map.toSeq |> Seq.collect (fun (_, a) -> a.Origins) |> Set.ofSeq

    /// CORS headers for an origin that is allowed, else none (FID-HOST-005).
    let corsHeaders (origin: string) =
        [ "Access-Control-Allow-Origin", origin
          "Vary", "Origin"
          "Access-Control-Allow-Methods", "POST, OPTIONS"
          "Access-Control-Allow-Headers", "content-type"
          "Access-Control-Max-Age", "600" ]

    /// Token responses must never be cached (RFC 6749 section 5.1).
    let private baseHeaders =
        [ "Cache-Control", "no-store"
          "Pragma", "no-cache"
          "X-Content-Type-Options", "nosniff" ]

    let private json = [ "Content-Type", "application/json" ]

    /// Which origin, if any, gets CORS headers: an origin registered for the
    /// named application, or, when no registered application is named, an
    /// origin registered for any application.
    let private corsOrigin (configuration: Configuration) (request: HostRequest) =
        match header "Origin" request with
        | None -> None
        | Some origin ->
            match Protocol.application request.Body |> Option.bind (Exchange.registered configuration) with
            | Some application -> if Exchange.originAllowed application (Some origin) then Some origin else None
            | None -> if (allOrigins configuration).Contains origin then Some origin else None

    let private respond configuration request status body =
        let cors = corsOrigin configuration request |> Option.map corsHeaders |> Option.defaultValue []
        let content = if String.IsNullOrEmpty body then [] else json

        { Status = status
          Headers = baseHeaders @ content @ cors
          Body = body }

    let private refuse configuration request refusal =
        respond configuration request (status refusal) (encodeRefusal refusal)

    /// The operation a path names: `token`, `refresh`, `revoke` or `unknown`.
    let operationName (path: string) =
        match path with
        | p when p = TokenPath -> "token"
        | p when p = RefreshPath -> "refresh"
        | p when p = RevokePath -> "revoke"
        | _ -> "unknown"

    let private audit configuration (request: HostRequest) outcome =
        { Operation = operationName request.Path
          Application =
            Protocol.application request.Body
            |> Option.filter (fun name -> (Exchange.registered configuration name).IsSome)
          Outcome = outcome }

    let private isJson (request: HostRequest) =
        match header "Content-Type" request with
        | Some value -> value.Trim().StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
        | None -> false

    let private decode (request: HostRequest) =
        if Encoding.UTF8.GetByteCount request.Body > MaxBodyBytes || not (isJson request) then
            None
        else
            match request.Path with
            | p when p = TokenPath -> decodeTokenRequest request.Body |> Option.map Exchange.ExchangeCode
            | p when p = RefreshPath -> decodeRefreshRequest request.Body |> Option.map Exchange.RefreshTokens
            | p when p = RevokePath -> decodeRevokeRequest request.Body |> Option.map Exchange.RevokeToken
            | _ -> None

    let private knownPath (path: string) =
        path = TokenPath || path = RefreshPath || path = RevokePath

    let private preflight configuration (request: HostRequest) =
        match header "Origin" request with
        | Some origin when (allOrigins configuration).Contains origin ->
            { Status = 204; Headers = baseHeaders @ corsHeaders origin; Body = "" }
        | _ ->
            { Status = 403; Headers = baseHeaders @ json; Body = encodeRefusal OriginNotAllowed }

    /// The answer when the host itself fails unexpectedly: nothing about the
    /// failure, and no CORS headers.
    let internalError =
        { Status = 500
          Headers = baseHeaders @ json
          Body = "{\"error\":\"internal_error\"}" }

    /// Answers one request.
    let handle (configuration: Configuration) (request: HostRequest) : Effect<HostResponse * AuditRecord> =
        let finish response outcome = Done(response, audit configuration request outcome)

        let refused refusal =
            finish (refuse configuration request refusal) (code refusal)

        match request.Method.ToUpperInvariant(), knownPath request.Path with
        | _, false -> refused NotFound
        | "OPTIONS", true ->
            let response = preflight configuration request
            finish response (if response.Status = 204 then "preflight" else code OriginNotAllowed)
        | "POST", true ->
            match decode request with
            | None -> refused MalformedRequest
            | Some operation ->
                Exchange.run configuration (header "Origin" request) operation
                |> Effect.map (fun result ->
                    let response, outcome =
                        match result with
                        | Ok(Exchange.SignedIn(grant, identity)) -> respond configuration request 200 (encodeGrant grant (Some identity)), "signed_in"
                        | Ok(Exchange.Refreshed grant) -> respond configuration request 200 (encodeGrant grant None), "refreshed"
                        | Ok Exchange.Revoked -> respond configuration request 204 "", "revoked"
                        | Error refusal -> refuse configuration request refusal, code refusal

                    response, audit configuration request outcome)
        | _, true -> refused MethodNotAllowed
