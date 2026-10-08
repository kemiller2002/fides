namespace Fides.Hosting

open System
open Aegis

/// Unexpected operational failures at the exchange host, classified through
/// Aegis (FID-EXC-005). Typed refusals stay in the protocol; only what the
/// host did not expect, and provider or configuration failures an operator
/// must see, become Aegis faults.
module Diagnostics =
    /// Stable fault codes. Messages may change; codes must not.
    let unexpected = FaultCode "FIDES.HOST.UNEXPECTED"
    let providerUnavailable = FaultCode "FIDES.PROVIDER.UNAVAILABLE"
    let providerContractViolation = FaultCode "FIDES.PROVIDER.CONTRACT_VIOLATION"
    let configurationUnavailable = FaultCode "FIDES.CONFIGURATION.UNAVAILABLE"

    /// The failure Aegis records in place of the original exception: its type
    /// only. An exception message can quote a request, and a request can hold
    /// a code, a verifier or a token, so the message never reaches a sink.
    type WithheldFailure(kind: string) =
        inherit Exception($"{kind} (message withheld)")

    /// Aegis for the exchange host. Delivery is blocking, so a host that is
    /// frozen right after it answers (AWS Lambda) loses no fault.
    let configure (version: string option) (sinks: Sinks.Sink list) =
        { Aegis.configure "Fides.Exchange" version sinks with Persistence = Blocking }

    /// The scope of one request: the operation, which is public.
    let scope (config: AegisConfig) (operation: string) =
        Aegis.scope config $"Fides.Exchange.{operation}" (Map.ofList [ "operation", Public operation ])

    /// The failure an exception stands for: a task or async boundary wraps it
    /// in an AggregateException holding exactly one inner exception.
    let rec underlying (error: exn) =
        match error with
        | :? AggregateException as aggregate when aggregate.InnerExceptions.Count = 1 ->
            underlying aggregate.InnerExceptions[0]
        | error -> error

    /// Classifies an exception that escaped the exchange.
    let classify (config: AegisConfig) (scope: Scope) (escaped: exn) : Fault =
        let error = underlying escaped
        let category = if Aegis.isProgrammingDefect error then ProgrammingDefect else InfrastructureFailure

        Aegis.faultOf
            config
            scope
            unexpected
            category
            FaultSeverity.Error
            OperationOnly
            UnknownPersistence
            (Retry(1, Immediate))
            "Sign-in is unavailable right now. Try again shortly."
            (WithheldFailure(error.GetType().FullName |> Option.ofObj |> Option.defaultValue "exception"))

    /// The fault, if any, an answered request's outcome means for an operator.
    let outcomeFault (config: AegisConfig) (scope: Scope) (outcome: string) =
        let fault code category severity persistence recovery message =
            Some(Aegis.faultOf config scope code category severity FeatureUnavailable persistence recovery message (WithheldFailure outcome))

        match outcome with
        | "provider_unavailable" ->
            fault providerUnavailable IntegrationFailure FaultSeverity.Warning Transient (Retry(3, Exponential(TimeSpan.FromSeconds 1.))) "GitHub is unavailable right now."
        | "provider_contract_violation" ->
            fault providerContractViolation IntegrationFailure FaultSeverity.Error Persistent ManualIntervention "GitHub answered in a way Fides does not accept."
        | "configuration_unavailable" ->
            fault configurationUnavailable ConfigurationFailure FaultSeverity.Critical RequiresIntervention ManualIntervention "The exchange's client secret is unavailable or was rejected."
        | _ -> None
