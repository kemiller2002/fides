---
id: FIDES-READINESS-GAP-REPORT
title: Fides readiness and gap report
status: draft
version: 0.1.0
created: 2026-11-09
updated: 2026-11-09
owners:
  - fides
related_documents:
  - docs/requirements/FIDES-REQUIREMENTS.md
  - docs/requirements/TRACEABILITY-MATRIX.md
  - docs/security/THREAT-MODEL.md
tags: [readiness, gaps, security, report]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:52.778Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
    EXE-20261109T154841724Z-a435eb94:
      operations: [modified]
      at: 2026-11-09T15:50:42.573Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Record the owner's acceptance of DF-FIDES-2026-0010..0015 on 2026-11-09 (WI-0023)"
---

# Fides readiness and gap report (WI-0022, 2026-11-09)

## 1. Verdict

The requirements baseline is **complete enough to resume implementation**.
It is checked by tests for internal consistency and status honesty. It is
**not** a statement that Fides is secure. Fides 0.2.0 is fit for a
single-application pilot with memory-only retention and no tenant
separation. It is **not ready** for:

- several applications sharing one environment;
- several organizations;
- production use with persistent retention.

These gaps are listed below. Nothing is deployed. No property of real
GitHub has been verified.

## 2. Numbers

| | Count |
|---|---:|
| Requirements | 130 |
| Retained unchanged from 0.2.0 | 25 |
| Revised (FID-TB-004, FID-EXC-003, FID-HOST-003, FID-CLI-002) | 4 |
| Added | 101 |
| Implemented and verified | 44 |
| Implemented, unverified | 16 |
| Missing | 51 (33 before the owner's decisions of 2026-11-09) |
| Blocked (external party or unapproved targets) | 12 (32 before) |
| Deferred | 7 (5 before) |
| P0 / P1 / P2 / P3 | 54 / 55 / 16 / 5 |
| P0 not yet verified | 27 |
| Requirements that need live-provider evidence (none has it) | 26 |

Per-family tables are in the [matrix](TRACEABILITY-MATRIX.md).

No requirement of 0.2.0 was removed. One item is **obsolete**: decision
FID-D-005 (the build order) is complete and is now marked historical.

Of the 44 verified requirements, 29 are the 0.2.0 requirements and 15 are new. Twelve of the new ones are verified by existing tests:

- FID-IDN-001, 002, 005
- FID-SES-007
- FID-DATA-001
- FID-OPS-001, 005, 008
- FID-REL-003, 004, 005, 008

Each of these tests got a `Verifies` trait only after its content was read and found to prove the requirement. The other three (FID-TEST-003..005) are verified by the new catalogue gates.

## 3. Existing behaviour that violates the new requirements

| # | Violation | Requirement | Severity | Evidence |
|---|---|---|---|---|
| V1 | The exchange keys provider clients by provider and accepts any registered application's refresh or revocation of any token (cross-application confused deputy) | FID-AUZ-009, FID-DATA-006 | High | `src/Fides/Configuration.fs` (`Clients: Map<ProviderId, ProviderClient>`), `src/Fides/Exchange.fs` (`RefreshTokens`) |
| V2 | One GitHub App per environment serves every application, so applications share token reach | FID-DATA-006 | High | `docs/hosting/AWS.md` §1 |
| V3 | The client compares absolute expiries from the exchange's clock with the browser clock | FID-REL-006 | Medium | `src/Fides/GitHub.fs` (`receivedAt.AddSeconds`), `src/Fides.Client/Tokens.fs` |
| V4 | One 401 marks the session revoked without trying a refresh | FID-SES-005 | Medium | `FidesClient.ReportUnauthorized` |
| V5 | The required-repository check runs at sign-in only | FID-AUZ-008 | Medium | `Exchange.run` (`RefreshTokens`) |
| V6 | A failed re-sign-in sets `provider_unavailable` over a live session | FID-IDN-009 | Low | `completeCallback` |
| V7 | Audit lines carry no correlation id or principal | FID-OPS-004, FID-OPS-007 | Medium | `Host.auditLine` |
| V8 | Persistent retention stores a six-month refresh token in local storage | FID-SES-008 (pending decision) | High if enabled | `FidesClient.storeFor` |

No test was weakened, and no requirement was redefined, to accommodate
these violations.

## 4. Critical security gaps

1. **Application permissions finer than a repository cannot be enforced.**
   The user holds a GitHub token and can call GitHub directly
   (SEC-THR-109; DF-FIDES-2026-0013). The mission requires "server-side
   enforcement for all protected operations". That cannot be met for
   browser-held tokens without a server-side enforcement point.
2. **No tenant model.** Cross-tenant isolation is unknown (SEC-THR-108).
3. **Cross-application token use.** See V1 and V2.
4. **Unverified provider assumptions.** The following are verified only
   against Fides's own simulator: single-use codes, PKCE enforcement,
   refresh rotation, and whether a refresh grant dies when the access token
   is revoked. Sign-out may leave a usable refresh grant (SEC-THR-118).
5. **Token exposure to script.** This holds in every retention mode, and at
   six-month scale in persistent mode. There is no CSP requirement on
   consumers yet (FID-UX-014).
6. **No incident-response runbooks, no rotation rehearsal and no deployment
   smoke test.** See FID-OPS-012..014.

## 5. Contradictions and unverified claims found

- **Strata.** The mission assumed Strata provides "approved, restricted
  operations". Strata 0.1.1 is a PostgreSQL schema-management CLI (grants
  and row-level policies as schema objects) with no runtime operation API,
  and no Fides consumer uses PostgreSQL. FID-DATA-005 is therefore
  deferred.
- **Dokimos.** The mission asked to use Dokimos to validate requirement
  coverage. Fides pins Dokimos 0.2.0, which cannot read requirement IDs.
  Coverage is enforced by xUnit gates whose results Dokimos ratchets.
  Dokimos 0.11.1's requirement verification needs an upgrade
  (FID-TEST-014).
- **Forma.** Forma 0.14.0's default palette is still Echelon's, so Fides
  must always supply a brand manifest (FID-UX-003).
- **Aegis.** Aegis has no browser sink, so client faults cannot reach Aegis
  (FID-UX-012). Aegis source is at 2.0.0 (untagged); echelon-current pins
  1.0.0, as Fides does.
- **Stale pins.** `context/CURRENT-STATE.md` recorded echelon-current 1.3.0
  and Ordo 1.5.0. echelon-current now resolves 1.37.2, with Praxis 3.25.0,
  Ordo 1.20.0, Dokimos 0.11.1 and Arca 0.7.0. Arca's token-provider port is
  unchanged since 0.1.0. The stale pins are WI-0034's first step.
- **Trust-boundary model §6.** The model implies that sign-out revocation
  is complete. That is unverified (FID-TEST-012).

## 6. Decisions (accepted by the owner on 2026-11-09, WI-0023)

| Record | Question | Recommendation |
|---|---|---|
| DF-FIDES-2026-0010 | Authentication-only, or also an authorization engine? | Authentication-only; no central service; a shared decision vocabulary as a contract |
| DF-FIDES-2026-0011 | Where does organization membership live? | Not in Fides; tenant = App installation plus Arca repositories |
| DF-FIDES-2026-0012 | Can stateless exchange meet replay and revocation needs? | Mostly; keep it, verify GitHub live, and state the supersession triggers |
| DF-FIDES-2026-0013 | Does GitHub-backed storage conflict with permissions and tenancy? | Yes; repository permission is the enforced boundary, with one App per isolation domain |
| DF-FIDES-2026-0014 | Do services need separate authentication? | Yes; installation tokens minted in the service's runtime |
| DF-FIDES-2026-0015 | Are the retention modes secure enough? | Restrict persistent mode now; design a sealed HttpOnly refresh cookie next; require a strict CSP |

All six recommendations were accepted as written. The owner still needs
to approve the proposed latency and availability
targets (FID-REL-001..002) and provide a test GitHub App plus a test AWS
environment (FID-TEST-010, FID-OPS-013).

## 7. Recommended implementation sequence

1. Owner decisions (WI-0023), in parallel with steps 2 and 3.
2. Client correctness defects (WI-0024): one clock, 401 handling, outage
   state.
3. Application-bound exchange (WI-0025): a versioned protocol change.
4. Live-provider suite as soon as the test App exists (WI-0026); fix
   sign-out per its result.
5. Adversarial suite (WI-0027).
6. Operational security (WI-0030), identity model (WI-0028), sessions
   (WI-0029).
7. Pin upgrades and Dokimos requirement verification (WI-0034), then the
   Forma and Limen UI (WI-0031).
8. Reliability targets after a deployed test environment exists (WI-0033).

## 8. Tests performed for this report

| Run | Result |
|---|---|
| Baseline before changes: `dotnet test Fides.slnx -c Release` | 312 passed, 0 failed (228 in Fides.Tests, 84 in Fides.Hosting.Aws.Tests) |
| After changes: the full suite, including the new catalogue gates | See the WI-0022 completion record and the pull request |
| Live-provider, deployment and browser accessibility tests | Not performed: none exists, and no environment is available |
