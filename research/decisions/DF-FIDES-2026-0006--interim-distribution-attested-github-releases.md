---
id: DF-FIDES-2026-0006
title: Until nuget.org is set up, Fides packages are distributed as Sigstore-attested GitHub release assets with an echelon-registry entry
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
tags: [release, distribution, supply-chain]
provenance:
  contributions:
    EXE-20261008T084302418Z-58dc4016:
      operations: [created]
      at: 2026-10-08T08:46:19.552Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "User decision of 2026-10-08 recorded for WI-0003"
---

# DF-FIDES-2026-0006 — Interim distribution through attested GitHub releases

- **Date:** 2026-10-08
- **Status:** accepted (user decision of 2026-10-08)

## Context

The Echelon `nuget-library` release contract publishes to nuget.org through
Trusted Publishing. The user has not set up nuget.org yet and asked that the
work not wait for it. Arca follows the same interim path.

## Decision

1. Until nuget.org is set up, each release publishes the `.nupkg` files,
   `checksums.txt` and the `echelon-release.json` manifest as assets of an
   immutable GitHub release, each with a GitHub build-provenance attestation
   (Sigstore-signed, verifiable with `gh attestation verify`).
2. Each released version is recorded in an echelon-registry entry that names
   the GitHub release as its distribution source.
3. Publishing to nuget.org is added, not substituted, once the user sets the
   `NUGET_USER` variable and the Trusted Publishing policy.
4. If Conditor's scaffold needs a change to support this path, that change
   belongs to the Arca agent's work in Conditor; Fides does not duplicate it.

## Consequences

- Consumers pin an exact version and verify the attestation before use.
- The release workflow publishes without a nuget.org credential.
