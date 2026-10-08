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

    /// Answers a request and returns the response with its log line.
    let handle (configuration: Configuration) (ports: Ports) (request: Service.HostRequest) =
        task {
            let clock = Stopwatch.StartNew()
            let! response, audit = Runtime.run ports (Service.handle configuration request)
            return response, auditLine audit clock.Elapsed
        }
