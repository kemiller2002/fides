---
id: FIDES-HOSTING-ADAPTERS
title: Adding a hosting adapter
status: accepted
version: 1.0.0
created: 2026-10-08
updated: 2026-10-08
owners:
  - fides
related_documents:
  - docs/hosting/AWS.md
  - research/decisions/DF-FIDES-2026-0001--cloud-agnostic-exchange-aws-first.md
tags: [hosting, adapters, conformance, azure]
provenance:
  contributions:
    EXE-20261008T095109372Z-cd1d26a9:
      operations: [created]
      at: 2026-10-08T09:53:39.209Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Adapter contract and conformance suite guide (WI-0010, FID-HOST-004)"
---

# Adding a hosting adapter

The exchange core (`src/Fides`) is a pure function from a host-neutral
request to an effect producing a host-neutral response
(`Service.handle`). `src/Fides.Hosting` runs those effects with an
HttpClient, a secret port and the clock. An adapter adds only:

1. **Translation** from the platform's request to `Service.HostRequest`,
   and from `Service.HostResponse` back. Header names arrive in whatever case
   the platform uses; `Service.header` matches them without case.
2. **A secret port**: `SecretReference -> Task<Result<ClientSecret, unit>>`
   over the platform's secret store, never throwing, wrapped in
   `SecretCache.cached`.
3. **An entry point** that reads the configuration document, refuses to
   start if it is invalid, and logs only `Host.auditLine`.
4. **Infrastructure as code** with the account, region, domain and secret as
   parameters (DF-FIDES-2026-0005).

It must not change `src/Fides` (FID-HOST-004).

## Conformance (FID-TEST-002)

Every adapter passes `Fides.Acceptance.Conformance`:

```fsharp
let myAdapter: ScenarioRunner.Implementation =
    fun secretAvailable world call ->
        // Build the platform's request from `call`, run the adapter with its
        // real HttpClient path pointed at `new SimulatorHttpHandler(world)`,
        // and return the simulated world and what the adapter answered.
        ...

[<Theory; MemberData(...)>]
let ``my adapter conforms`` (id: string) =
    Assert.Empty(Conformance.scenarioFailures ScenarioRunner.core myAdapter (scenario id))
```

An adapter conforms when it satisfies all 30 exchange acceptance scenarios
and, for every step and every extra case (preflights, header-name case,
unknown paths and methods, empty, non-object and non-ASCII bodies), answers
with exactly the core's status, body and headers. The suite references no
platform, so the same tests run for every adapter. The AWS adapter's run is
`tests/Fides.Hosting.Aws.Tests/ConformanceTests.fs`.

## Azure (deferred, WI-0011)

An Azure Functions adapter (isolated worker, HTTP trigger) would translate
`HttpRequestData`/`HttpResponseData`, read the client secret from Key Vault
(`SecretClient.GetSecretAsync`) through a managed identity, keep the
configuration in an app setting, and ship a Bicep template with the same
parameters as the AWS template. It is built only when the operator needs
Azure.
