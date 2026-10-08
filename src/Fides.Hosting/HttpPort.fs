namespace Fides.Hosting

open System
open System.Net.Http
open System.Text
open System.Threading
open Fides

/// Sends provider requests with an HttpClient.
module HttpPort =
    /// The HttpRequestMessage for a provider request.
    let message (request: ProviderRequest) =
        let message = new HttpRequestMessage(HttpMethod request.Method, request.Url)

        for name, value in request.Headers do
            message.Headers.TryAddWithoutValidation(name, value) |> ignore

        match request.Body with
        | NoBody -> ()
        | Form pairs -> message.Content <- new FormUrlEncodedContent(pairs |> List.map (fun (k, v) -> Collections.Generic.KeyValuePair(k, v)))
        | Json text -> message.Content <- new StringContent(text, Encoding.UTF8, "application/json")

        message

    /// Sends with a per-request timeout. It never throws: a timeout is
    /// `Failed TimedOut` and any other transport failure is `Failed Unreachable`.
    /// Exception details are dropped because they can quote the request.
    let send (client: HttpClient) (timeout: TimeSpan) (request: ProviderRequest) =
        task {
            use message = message request
            use cancellation = new CancellationTokenSource(timeout)

            try
                use! response = client.SendAsync(message, cancellation.Token)
                let! body = response.Content.ReadAsStringAsync(cancellation.Token)
                return Responded { Status = int response.StatusCode; Body = body }
            with
            | :? OperationCanceledException -> return Failed TimedOut
            | :? HttpRequestException -> return Failed Unreachable
            | :? AggregateException as e when (e.InnerException :? OperationCanceledException) -> return Failed TimedOut
        }

    /// An HttpClient suited to provider calls: bounded response size, no
    /// automatic redirects (a provider endpoint never redirects a token call).
    let client () =
        let handler = new SocketsHttpHandler(AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes 5.)
        let client = new HttpClient(handler)
        client.MaxResponseContentBufferSize <- 1048576L
        client
