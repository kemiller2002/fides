---
id: FIDES-VERIFICATION-PLAN
title: Fides Dokimos verification plan
status: draft
version: 0.1.0
created: 2026-11-09
updated: 2026-11-09
owners:
  - fides
related_documents:
  - docs/requirements/FIDES-REQUIREMENTS.md
  - docs/requirements/TRACEABILITY-MATRIX.md
  - .dokimos/policy.json
tags: [verification, dokimos, testing]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:51.456Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
---

# Fides verification plan

How every requirement is verified, by which kind of test, and how Dokimos
gates it.

## 1. Verification levels

| Level | What runs | Proves | Never proves |
|---|---|---|---|
| `document` | A test reads a repository document | The definition exists | Behaviour |
| `unit` | Pure functions and the client against `FakeBrowser` | Fides's logic | Any platform or provider behaviour |
| `integration` | Several components or the CI pipeline together | Wiring | Provider behaviour |
| `simulated-provider` | The exchange and client against Fides's own GitHub simulator (`tests/Fides.Acceptance/GitHubSimulator.fs`) | Fides's handling of every provider answer it models | That GitHub answers that way |
| `emulated-host` | The built Lambda `bootstrap` against an emulated Runtime API, and the function over real HTTP | The deployable artefact's wiring | AWS itself |
| `browser-wasm` | The client in the trimmed browser-wasm runtime under Node (`scripts/verify-wasm.sh`) | The client in the real runtime | A real browser's storage, CSP or tabs |
| `live-provider` | Against real GitHub with a test App, on demand | Provider assumptions | — |
| `manual-accessibility` | Recorded keyboard, screen-reader and audit sessions | Accessibility in practice | — |

**Rule (FID-TEST-004):** a requirement whose `Levels required` includes
`live-provider` cannot be `implemented-verified` until a live test ran.
Today no live test exists: 26 requirements need `live-provider` and none
has it, so none of them is `implemented-verified`.

## 2. Gates in CI

| Gate | Where | Fails when |
|---|---|---|
| Requirement accounting (existing) | `RequirementsTraceabilityTests` | A requirement is neither planned by open work nor verified; a test claims an undeclared requirement |
| Catalogue structure (FID-TEST-003) | `RequirementsCatalogueTests` | A field is missing or a value not allowed; a dependency is dangling; a statement is not normative |
| Status honesty (FID-TEST-004) | `RequirementsCatalogueTests` | `implemented-verified` without a test or below a required level; `missing` while a test claims it; not verified and not planned; a P0 security requirement without an adversarial test |
| Matrix freshness (FID-TEST-005) | `RequirementsCatalogueTests` | `TRACEABILITY-MATRIX.md` differs from the catalogue |
| Acceptance catalogue (FID-TEST-001) | `AcceptanceCatalogueTests` | An issue #1 category, refusal code or session state has no scenario |
| Adapter conformance (FID-TEST-002) | `ConformanceTests` | An adapter differs from the core |
| Dokimos quality gate | `.github/workflows/dokimos.yml` | Compiler errors or failed tests (ratchets at 0) |

## 3. How Dokimos validates requirement coverage today

Fides pins **Dokimos 0.2.0**, which evaluates build and test evidence only
(`quality.snapshot`, `compare`, `evaluate`, `history`). It has no
requirement-coverage command. Coverage is therefore validated *through*
Dokimos rather than *by* it: the catalogue gates above run inside the
`dotnet test` step whose TRX results Dokimos ratchets with
`tests.failed = 0`. A requirement-status lie fails a test, which fails the
Dokimos gate.

This is the honest limit: Dokimos 0.2.0 does not itself read requirement
IDs.

## 4. Planned: Dokimos requirement verification (FID-TEST-014, WI-0034)

Dokimos 0.11.1 (in echelon-current 1.37.2) adds
`dokimos verification requirements`, which reads a Praxis
`praxis.verification-manifest/1`, checks `data-requirement-id` hooks in the
UI source, and takes browser observations and Playwright results, with
statuses PASS / FAIL / BLOCKED / NOT_RUN / NOT_APPLICABLE. Steps:

1. Move the Conditor pins (`conditor.json`: Praxis 3.7.2, Dokimos 0.2.0,
   Arca 0.1.0) to echelon-current, re-running the full suite.
2. Express the catalogue as a `praxis.requirement-catalog/1` with
   `idPattern` `^FID-[A-Z]+-\d{3}$`, sourced from `FIDES-REQUIREMENTS.md`.
3. Compile the verification manifest for FID-UX requirements and run
   `dokimos verification requirements` against the Forma screens.
4. Keep the xUnit gates for protocol and server requirements, which a
   browser-oriented verifier does not cover.

## 5. Mandatory adversarial tests

| Category | Existing tests | Missing tests | Level needed |
|---|---|---|---|
| Cross-tenant access | none | FID-TEST-006 | simulated-provider, live-provider |
| Privilege escalation | none | FID-TEST-007 | unit, live-provider |
| Token replay | `replayed-state`, `replayed-code`, single-flight refresh | Live single-use (FID-TEST-010, FID-TEST-011) | live-provider |
| Expired tokens | `expired-session-refreshes`, `expired-refresh-token`, `expired-state` | Clock-skew cases (FID-REL-006), boundary instants (FID-REL-007) | unit |
| Revoked credentials | `revoked-identity`, `revoked-refresh`, `revoked-identity-reported` | 401-then-refresh (FID-SES-005); live revocation (FID-TEST-012, FID-TEST-013) | unit, live-provider |
| Malformed callbacks | `invalid-state`, `missing-state`, `unsolicited-callback`, non-JSON body | Generated inputs (FID-TEST-008); cross-application callback (FID-IDN-004) | unit, simulated-provider |
| Missing permissions | `repository-access-denied` at sign-in | At refresh (FID-AUZ-008); App permission set (FID-AUZ-010) | simulated-provider, live-provider |
| Unauthorized data access | Arca bridge failure mapping | Through Arca with each refusal (FID-TEST-009); directly against GitHub (FID-AUZ-002) | simulated-provider, live-provider |
| Provider failures | `provider-outage`, `callback-provider-outage`, `expired-session-outage`, 5xx/429/transport classification | Live failure shapes (FID-TEST-010) | live-provider |
| Cross-application confused deputy | none | FID-AUZ-009 | unit, simulated-provider |

Every test in this table must exercise the enforcement boundary: the
exchange's HTTP surface, the client's public API, or GitHub itself. A test
that hides a button and asserts it is hidden verifies nothing here.

## 6. Live-provider suite (FID-TEST-010..013)

Run on demand, never on pull requests, against a test-environment GitHub
App whose credentials come from the CI secret store (never the
repository). Each run writes an evidence record with the App id, the time
and each assertion's outcome. Blocked until the operator registers the
test App; Fides does not create or rotate credentials.
