/// Local emulation of the AWS Lambda runtime: the built `bootstrap` runs as a
/// real process against an in-test implementation of the Lambda Runtime API,
/// exactly as Lambda's provided.al2023 runtime would drive it. Nothing is
/// deployed and nothing reaches AWS.
module Fides.Hosting.Aws.Tests.RuntimeEmulationTests

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading.Tasks
open Xunit
open Fides.Acceptance
open Fides.Acceptance.Scenarios

let private freePort () =
    let listener = new TcpListener(IPAddress.Loopback, 0)
    listener.Start()
    let port = (listener.LocalEndpoint :?> IPEndPoint).Port
    listener.Stop()
    port

let private bootstrapPath () =
    let local = Path.Combine(AppContext.BaseDirectory, "bootstrap.dll")

    if File.Exists local then
        local
    else
        failwith $"bootstrap.dll not found next to the tests ({AppContext.BaseDirectory})"

/// A minimal Lambda Runtime API on the thread pool: hands out `events` one at
/// a time, records each posted response, and signals when the function polls
/// again after the last event.
type private RuntimeApi(port: int, events: string list) =
    let listener = new HttpListener()
    let responses = ConcurrentQueue<string>()
    let drained = TaskCompletionSource()
    let pending = ConcurrentQueue<string>(events)
    let mutable issued = 0

    let answer (context: HttpListenerContext) =
        task {
            let path = context.Request.Url |> Option.ofObj |> Option.map _.AbsolutePath |> Option.defaultValue ""

            if path.EndsWith "/invocation/next" then
                match pending.TryDequeue() with
                | true, event ->
                    issued <- issued + 1
                    let bytes = Encoding.UTF8.GetBytes event
                    context.Response.AddHeader("Lambda-Runtime-Aws-Request-Id", $"request-{issued}")
                    context.Response.AddHeader("Lambda-Runtime-Deadline-Ms", string (DateTimeOffset.UtcNow.AddMinutes(1.).ToUnixTimeMilliseconds()))
                    context.Response.AddHeader("Lambda-Runtime-Invoked-Function-Arn", "arn:aws:lambda:us-east-1:000000000000:function:fides-exchange-test")
                    context.Response.ContentType <- "application/json"
                    context.Response.ContentLength64 <- int64 bytes.Length
                    do! context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length)
                    context.Response.Close()
                | _ ->
                    // Leave the poll open; the test ends the process.
                    drained.TrySetResult() |> ignore
            else
                use reader = new StreamReader(context.Request.InputStream)
                let! body = reader.ReadToEndAsync()
                responses.Enqueue(path + " " + body)
                context.Response.StatusCode <- 202
                context.Response.Close()
        }

    do
        listener.Prefixes.Add $"http://127.0.0.1:{port}/"
        listener.Start()

        Task.Run<unit>(fun () ->
            task {
                try
                    while listener.IsListening do
                        let! context = listener.GetContextAsync()
                        answer context |> ignore
                with _ ->
                    ()
            })
        |> ignore

    member _.Drained = drained.Task
    member _.Responses = responses.ToArray()

    interface IDisposable with
        member _.Dispose() = listener.Close()

/// Starts the bootstrap with both output streams drained, so it never blocks
/// on a full pipe. Returns the process and a reader for what it wrote.
let private start (port: int option) (configuration: string) =
    let info = ProcessStartInfo("dotnet", $"\"{bootstrapPath ()}\"")
    info.RedirectStandardError <- true
    info.RedirectStandardOutput <- true
    info.UseShellExecute <- false
    info.Environment["FIDES_CONFIGURATION"] <- configuration
    info.Environment["AWS_REGION"] <- "us-east-1"
    info.Environment["AWS_LAMBDA_FUNCTION_NAME"] <- "fides-exchange-test"

    match port with
    | Some port -> info.Environment["AWS_LAMBDA_RUNTIME_API"] <- $"127.0.0.1:{port}"
    | None -> info.Environment.Remove "AWS_LAMBDA_RUNTIME_API" |> ignore

    let output = StringBuilder()
    let proc = new Process(StartInfo = info)
    proc.OutputDataReceived.Add(fun e -> lock output (fun () -> output.AppendLine e.Data |> ignore))
    proc.ErrorDataReceived.Add(fun e -> lock output (fun () -> output.AppendLine e.Data |> ignore))

    if not (proc.Start()) then
        failwith "could not start bootstrap"

    proc.BeginOutputReadLine()
    proc.BeginErrorReadLine()
    proc, (fun () -> lock output (fun () -> output.ToString()))

[<Fact>]
[<Trait("Verifies", "FID-HOST-002")>]
let ``the bootstrap process answers API Gateway events through the Lambda Runtime API`` () : Task =
    task {
        let port = freePort ()

        let events =
            [ Events.json "OPTIONS" "/v1/token" "$default" [ "origin", "https://chrona.example" ] None false
              Events.json
                  "POST"
                  "/v1/token"
                  "$default"
                  [ "content-type", "application/json"; "origin", "https://chrona.example" ]
                  (Some(tokenBody chrona "sim-code-0001" verifier "https://attacker.example/cb"))
                  false ]

        use api = new RuntimeApi(port, events)
        let proc, output = start (Some port) ExchangeFixture.configurationDocument
        use proc = proc

        try
            let! first = Task.WhenAny(api.Drained, Task.Delay(TimeSpan.FromSeconds 60.))

            if not (Object.ReferenceEquals(first, api.Drained)) then
                failwith ("the bootstrap did not poll for every event: " + output ())

            let answers = api.Responses
            Assert.Equal(2, answers.Length)
            Assert.StartsWith("/2018-06-01/runtime/invocation/request-1/response", answers[0])
            Assert.Contains("\"statusCode\":204", answers[0])
            Assert.Contains("https://chrona.example", answers[0])
            Assert.StartsWith("/2018-06-01/runtime/invocation/request-2/response", answers[1])
            Assert.Contains("\"statusCode\":400", answers[1])
            Assert.Contains("redirect_uri_not_allowed", answers[1])
        finally
            if not proc.HasExited then
                proc.Kill true
    }

[<Fact>]
let ``the bootstrap refuses to start with an invalid configuration`` () =
    let proc, output = start None """{"applications":[]}"""
    use proc = proc

    if not (proc.WaitForExit 60000) then
        proc.Kill true
        failwith "the bootstrap did not exit"

    proc.WaitForExit()
    Assert.Equal(1, proc.ExitCode)
    Assert.Contains("Fides configuration is invalid", output ())
