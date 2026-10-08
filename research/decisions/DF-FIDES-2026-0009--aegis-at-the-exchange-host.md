---
id: DF-FIDES-2026-0009
title: Unexpected failures at the exchange host are classified through Aegis 1.0.0; exception messages never reach a sink; the client is not bound to Aegis yet
status: accepted
version: 1.0.0
created: 2026-10-08
updated: 2026-10-08
owners:
  - fides
review_cycle: on-trigger
supersedes: []
superseded_by: []
related_documents:
  - docs/requirements/FIDES-REQUIREMENTS.md
  - docs/architecture/EXCHANGE-PROTOCOL.md
tags: [diagnostics, aegis, security, hosting]
provenance:
  contributions:
    EXE-20261008T102659947Z-c29ed82a:
      operations: [created]
      at: 2026-10-08T10:31:03.810Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Aegis binding at the exchange host (WI-0014, FID-EXC-005)"
---

# DF-FIDES-2026-0009 — Aegis at the exchange host

- **Date:** 2026-10-08
- **Status:** accepted (implements FID-EXC-005's Aegis clause)

## Context

FID-EXC-005 requires unexpected failures to be classified through Aegis.
`EchelonFoundry.Aegis.Core` 1.0.0 is on nuget.org. (An earlier record in
this repository said Aegis was unpublished; that check had queried a
package id that does not exist, and the record is corrected.) Aegis 1.0.0
depends on FSharp.Core 10.1.400, which needs the .NET SDK 10.0.400 or later.

The pure core performs no I/O and cannot fail unexpectedly: every outcome is
typed. Unexpected failures can only arise where effects run, which is the
hosting runtime.

## Decision

1. `Fides.Hosting` references Aegis.Core 1.0.0. `Host.handle` runs the
   exchange inside `Aegis.captureAsync`. An exception that escapes becomes an
   Aegis fault `FIDES.HOST.UNEXPECTED` (programming defect or infrastructure
   failure) and the answer `500 {"error":"internal_error"}`.
2. Answered requests whose outcome an operator must see are reported to
   Aegis as faults: `FIDES.PROVIDER.UNAVAILABLE`,
   `FIDES.PROVIDER.CONTRACT_VIOLATION` and `FIDES.CONFIGURATION.UNAVAILABLE`.
   The caller's answer does not change.
3. Aegis records the exception's **type only**. Its message is withheld,
   because a message can quote a request and a request can hold a code, a
   verifier or a token. Scope context holds only the operation name.
4. Delivery is blocking, so AWS Lambda, which freezes the process after the
   response, loses no fault. The deployed function writes faults to standard
   error, which Lambda sends to the function's log group.
5. The pure core and the WebAssembly client do not reference Aegis. The
   client's ports must not throw by contract; binding the client to Aegis
   waits for a browser sink.
6. `global.json` requires SDK 10.0.400 or later.

## Consequences

- Operators see provider outages and configuration faults as structured
  Aegis events with stable codes, next to the audit lines.
- A local build on an older SDK stops with a clear SDK message rather than a
  package downgrade error.
