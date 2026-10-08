---
id: DF-FIDES-2026-0002
title: Fides starts with GitHub as its only identity provider behind an extensible provider model; Helix keeps its own sign-in for now; Arca never depends on Fides
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
tags: [providers, github, scope, helix, arca]
provenance:
  contributions:
    EXE-20261008T082246199Z-61dcdd53:
      operations: [created]
      at: 2026-10-08T08:23:20.140Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Consolidated from issue #1, user decisions of 2026-10-08 and consumer sign-in requirements"
---

# DF-FIDES-2026-0002 — GitHub only, extensible provider model

- **Date:** 2026-10-08
- **Status:** accepted (user decisions of 2026-10-08)

## Decision

1. **Start small: GitHub only.** Arca, the shared data layer, stores data in
   GitHub and needs a GitHub token, so GitHub is the one provider Fides
   implements now (FID-PRV-002).
2. **Extensible provider model.** Providers sit behind an interface with
   declared capabilities, so others can be added later with no change to
   consuming applications beyond configuration (FID-PRV-001, FID-PRV-004).
3. **Helix keeps its own sign-in for now** and does not move to Fides yet.
   Fides's first consumers are Chrona, then Summa, then Signal.
4. **Arca does not depend on Fides.** Fides's client exposes a token provider
   that satisfies Arca's token-provider port (FID-CLI-001).

## Consequences

- The first release is small and is exercised by a real consumer: Chrona
  through Arca.
- Adding a provider is a Fides change only.
