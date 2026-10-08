/// An HttpMessageHandler backed by the GitHub simulator, so a hosting
/// adapter's real HttpClient path can run against simulated GitHub. It holds
/// the simulated world for the duration of one test.
namespace Fides.Acceptance

open System
open System.Net
open System.Net.Http
open System.Threading.Tasks
open Fides.Acceptance.GitHubSimulator

type SimulatorHttpHandler(initial: World) =
    inherit HttpMessageHandler()

    let mutable world = initial

    /// The simulated world as it is now.
    member _.World
        with get () = world
        and set value = world <- value

    override _.SendAsync(request, cancellationToken) =
        task {
            let content =
                match request.Content with
                | Null -> None
                | NonNull content -> Some content

            let! body =
                match content with
                | Some content -> content.ReadAsStringAsync(cancellationToken)
                | None -> Task.FromResult ""

            let headers =
                [ for h in request.Headers do
                      for v in h.Value -> h.Key, v
                  match content with
                  | Some content ->
                      for h in content.Headers do
                          for v in h.Value -> h.Key, v
                  | None -> () ]

            let simulated =
                { Method = request.Method.Method
                  Url = string request.RequestUri
                  Headers = headers
                  Body = body }

            let next, outcome = handle world simulated
            world <- next

            match outcome with
            | SimOutcome.Unreachable -> return raise (HttpRequestException "simulated network failure")
            | SimOutcome.Responded(status, text) ->
                let response = new HttpResponseMessage(enum<HttpStatusCode> status)
                response.Content <- new StringContent(text)
                return response
        }
