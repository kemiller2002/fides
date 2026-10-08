namespace Fides.Hosting

open System
open System.Diagnostics
open Fides
open Fides.JsonWrite

/// What every hosting adapter does with a request once it has translated it.
module Host =
    /// The providers the exchange can use.
    let catalog = ProviderCatalog.ofList [ GitHub.provider GitHub.githubDotCom ]

    /// Reads the configuration document; the problems name no secret.
    let configuration (document: string option) =
        match document with
        | None
        | Some "" -> Error [ "no configuration document was supplied" ]
        | Some document -> Configuration.parse catalog document

    /// The log line for one answered request: the audit record and the
    /// duration, and nothing from the request or response.
    let auditLine (audit: Service.AuditRecord) (elapsed: TimeSpan) =
        render (
            objectOf
                [ "operation", Some(String audit.Operation)
                  "application", audit.Application |> Option.map String
                  "outcome", Some(String audit.Outcome)
                  "durationMs", Some(Number(int64 elapsed.TotalMilliseconds)) ]
        )

    /// Answers a request and returns the response with its log line. An
    /// exception that escapes the exchange is captured and classified through
    /// Aegis and answered with `internal_error`; a provider or configuration
    /// failure is reported to Aegis as well (FID-EXC-005). Neither changes
    /// what the caller receives for an answered request.
    let handle (aegis: Aegis.AegisConfig) (configuration: Configuration) (ports: Ports) (request: Service.HostRequest) =
        task {
            let clock = Stopwatch.StartNew()
            let operation = Service.operationName request.Path
            let scope = Diagnostics.scope aegis operation

            let! result =
                Aegis.Aegis.captureAsync aegis scope (Diagnostics.classify aegis) (fun () ->
                    Runtime.run ports (Service.handle configuration request) |> Async.AwaitTask)
                |> Async.StartAsTask

            match result with
            | Ok(response, audit) ->
                match Diagnostics.outcomeFault aegis scope audit.Outcome with
                | Some fault -> Aegis.Aegis.report aegis (Aegis.FaultRecorded fault) |> ignore
                | None -> ()

                return response, auditLine audit clock.Elapsed
            | Error _ ->
                let audit: Service.AuditRecord =
                    { Operation = operation
                      Application = None
                      Outcome = "internal_error" }

                return Service.internalError, auditLine audit clock.Elapsed
        }
