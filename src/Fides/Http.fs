namespace Fides

/// A request body the provider understands.
type RequestBody =
    | NoBody
    | Form of (string * string) list
    | Json of string

/// An HTTP request Fides asks its host to send to a provider. Its body can
/// carry secrets, so it prints only its method and URL.
[<StructuredFormatDisplay("{Display}")>]
type ProviderRequest =
    { Method: string
      Url: string
      Headers: (string * string) list
      Body: RequestBody }

    member this.Display = $"{this.Method} {this.Url}"
    override this.ToString() = this.Display

/// A provider's HTTP response. Its body can carry tokens, so it prints only
/// its status.
[<StructuredFormatDisplay("{Display}")>]
type ProviderResponse =
    { Status: int
      Body: string }

    member this.Display = $"HTTP {this.Status}"
    override this.ToString() = this.Display

/// Why a provider request produced no response.
type TransportFailure =
    | TimedOut
    | Unreachable

/// What happened to a provider request.
type HttpOutcome =
    | Responded of ProviderResponse
    | Failed of TransportFailure
