/// Aegis with a sink that keeps every event it is given, for tests.
module Fides.Hosting.Aws.Tests.Collecting

open System.Collections.Concurrent
open Aegis

/// Aegis for the exchange host, with its events collected instead of written.
let aegis () =
    let events = ConcurrentQueue<string>()

    let sink: Sinks.Sink =
        { Name = "collector"
          Level = Sinks.Required
          Capabilities = []
          Write = fun json -> async { events.Enqueue json }
          WriteBatch = None }

    Fides.Hosting.Diagnostics.configure (Some "test") [ sink ], events
