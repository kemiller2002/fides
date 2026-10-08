---
id: DF-FIDES-2026-0008
title: The Fides client mirrors Arca's published token-provider port; a packaged bridge waits for Conditor's release-asset installation
status: accepted
version: 1.1.0
created: 2026-10-08
updated: 2026-10-08
owners:
  - fides
review_cycle: on-trigger
supersedes: []
superseded_by: []
related_documents:
  - docs/client/README.md
  - docs/architecture/EXCHANGE-PROTOCOL.md
  - research/decisions/DF-FIDES-2026-0002--github-only-extensible-provider-model.md
  - research/decisions/DF-FIDES-2026-0006--interim-distribution-attested-github-releases.md
tags: [client, arca, dependencies, distribution]
provenance:
  contributions:
    EXE-20261008T093828040Z-3faec4b1:
      operations: [created]
      at: 2026-10-08T09:48:36.466Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Client token provider mirrors Arca 0.1.0's port (WI-0009)"
    EXE-20261008T103225377Z-c76772c6:
      operations: [modified]
      at: 2026-10-08T10:34:37.403Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Packaged Arca bridge through Conditor's feed (WI-0016)"
---

# DF-FIDES-2026-0008 — The client mirrors Arca's port

- **Date:** 2026-10-08
- **Status:** accepted (coordination with the Arca agent's published work)

## Context

Arca's token-provider port is on arca main and released in
EchelonFoundry.Arca.Core 0.1.0 (checked 2026-10-08):
`TokenProvider = unit -> Async<Result<Arca.AccessToken, Arca.TokenUnavailable>>`
with `NoToken`, `Expired`, `Revoked` and `ProviderFailed of reason`. Arca
must never depend on Fides (DF-FIDES-2026-0002). Arca 0.1.0 is distributed
only as attested GitHub release assets; Arca's plan has consumers install
them through Conditor, and Conditor main does not do that yet. That work
belongs to the Arca agent.

## Decision

1. `Fides.Client.TokenProvider` mirrors Arca's port case for case, in Fides's
   own types, so `EchelonFoundry.Fides.Client` depends on neither Arca nor
   any unpublished package.
2. An outage during refresh is `ProviderFailed`, not `Expired`, matching
   Arca's distinction between a transient failure and a session that needs
   a new sign-in. The session scenario `expired-session-outage` and the
   protocol were updated to say so.
3. Applications bridge the two types in a few lines where they compose
   Fides and Arca (docs/client/README.md). A packaged bridge,
   `EchelonFoundry.Fides.Arca`, is a captured work item that starts once
   Conditor installs Arca's release assets, so Fides does not duplicate
   that installation path.

## Update (2026-10-08, WI-0016)

kemiller2002/conditor#59 now installs GitHub-release-asset NuGet libraries
into a pinned, verified local feed, and echelon-registry records arca 0.1.0
as an optional project binding. Fides opted in through `conditor upgrade
--current` (echelon-current 1.5.0), and the packaged bridge
`EchelonFoundry.Fides.Arca` maps the client's provider to Arca's, tested
against Arca's real types. The client itself still references neither.

## Consequences

- Neither library references the other.
- A change to Arca's port changes one bridge function, not the client.
