# Fides backlog plan

Captured 2026-10-08 (WI-0002) from [`FIDES-REQUIREMENTS.md`](FIDES-REQUIREMENTS.md). Build order, per the user: the minimal Fides slices (1-7, WI-0003..WI-0009) come before Chrona's sign-in work, then Summa and Signal. Helix is not a consumer for now (DF-FIDES-2026-0002). The test runner (`tests/Fides.Tests`) fails if any requirement is not named by an open work item.

| Order | Work item | Slice | Depends on |
|---:|---|---|---|
| 1 | WI-0003 | Fides slice 1: re-anchor the charter and context on SSO, with a context gate test (FID-CTX-001..003, issue #1) | - |
| 2 | WI-0004 | Fides slice 2: trust-boundary model and the decision on what GitHub authentication proves (FID-TB-001..006, issue #1) | WI-0003 |
| 3 | WI-0005 | Fides slice 3: executable acceptance tests before production code - invalid state/nonce, replay, wrong callback, revoked identity, expired session, repository access denial, provider outage (FID-TEST-001) | WI-0004 |
| 4 | WI-0006 | Fides slice 4: extensible provider model with GitHub as the only provider (FID-PRV-001..004, decision FID-D-002) | WI-0004 |
| 5 | WI-0007 | Fides slice 5: pure F# code-for-token exchange core - authorization code with PKCE, single-use state, replay refusal (FID-EXC-001..005) | WI-0005 and WI-0006 |
| 6 | WI-0008 | Fides slice 6: AWS hosting adapter - Lambda behind API Gateway, client secret from Secrets Manager/SSM, infrastructure as code per environment (FID-HOST-001..003, FID-HOST-005) | WI-0007 |
| 7 | WI-0009 | Fides slice 7: WASM client library and Arca token provider - sign-in, callback, session states, retention modes (FID-CLI-001..004) | WI-0007 |
| 8 | WI-0010 | Fides: shared hosting-adapter conformance suite (FID-TEST-002, FID-HOST-004) | WI-0008 |
| 9 | WI-0011 | (Deferred) Fides Azure hosting adapter - Azure Functions plus Key Vault (FID-HOST-004) | WI-0010 |
| 10 | WI-0012 | Fides: release workflow and echelon-registry entry for the client package and exchange artifact | WI-0009 |

The backlog itself lives in `.ros/work/queue.json` and is managed only through the Praxis CLI. This table is a readable snapshot from when the slices were captured.

## WI-0022 backlog (2026-11-09): from requirements baseline 0.3.0

Captured by WI-0022 from the expanded catalogue. Order is by risk and
dependency; P0 security defects first, decisions in parallel, UI after the
client contract is corrected. Nothing below has started.

| Order | Work item | Scope | Requirements | Depends on | Kind |
|---:|---|---|---|---|---|
| 1 | WI-0023 (**complete 2026-11-09**: all six accepted) | Owner decisions DF-FIDES-2026-0010..0015 | FID-AUZ-003..007, FID-AUZ-011, FID-ORG-*, FID-SVC-*, FID-SES-008, FID-SES-010, FID-DATA-003..004, FID-REL-001..002, FID-UX-011 | — | owner, blocking |
| 2 | WI-0024 | Client correctness: one clock for expiry, 401 → refresh before revoked, outage keeps state, boundary and restore tests | FID-REL-006..007, FID-REL-010, FID-SES-005, FID-SES-009, FID-IDN-004, FID-IDN-009 | — | P0 defect |
| 3 | WI-0025 | Application-bound exchange: client per application, bound refresh and revoke, user-token-only checks, required-repository re-check | FID-DATA-006, FID-AUZ-009, FID-DATA-008, FID-AUZ-008 | WI-0023 (DF-0013) for the App-per-domain policy; the code can start | P0 defect, versioned protocol change |
| 4 | WI-0026 | Live-provider contract suite; then sign-out grant fix | FID-TEST-010..013, FID-SES-004, FID-SES-006 | Operator registers a test GitHub App | blocked-external |
| 5 | WI-0027 | Adversarial suite: cross-tenant, privilege escalation, fuzzing, unauthorized data access | FID-TEST-006..009 | WI-0023, WI-0025, WI-0026 for live parts | P0 tests |
| 6 | WI-0030 | Operational security: correlation ids, audit principal, alarms, access-log pin, limits, supply chain, runbooks, smoke test, rotation rehearsal | FID-OPS-002..004, FID-OPS-006..007, FID-OPS-009..014 | WI-0028 for the principal | P1 |
| 7 | WI-0028 | Identity model: principal, authentication time, recovery guidance | FID-IDN-003, FID-IDN-006..008, FID-IDN-010 | — | P1 |
| 8 | WI-0029 | Session lifecycle: maximum lifetime, idle timeout, cross-tab lock, sign-out everywhere | FID-SES-001..003, FID-SES-010 | WI-0028, WI-0024 | P1 |
| 9 | WI-0032 | Authorization ownership and data authority deliverables | FID-AUZ-001..002, FID-AUZ-010, FID-DATA-002, FID-DATA-007 | WI-0023 | P0/P1 |
| 10 | WI-0034 | Dokimos requirement verification and Praxis catalogue; pin upgrades | FID-TEST-014, FID-OPS-011 | — | P1 |
| 11 | WI-0031 | Fides UI on Forma and Limen | FID-UX-001..015 | WI-0024, WI-0028, WI-0034 | P1 |
| 12 | WI-0033 | Reliability contracts and targets | FID-REL-001..002, FID-REL-009 | WI-0023 (targets), WI-0030 (smoke test) | P2 |
| 13 | WI-0035 | (Deferred) relational path through Strata | FID-DATA-005 | a relational store being approved | deferred |
| — | WI-0011 | (Deferred) Azure adapter | FID-HOST-004 | — | deferred |

The queue (`.ros/work/queue.json`) is authoritative; this table is a
snapshot. `RequirementsCatalogueTests` fails if any requirement that is
not verified stops being named by an open work item.

### Follow-up from the accepted decisions (WI-0023, 2026-11-09)

| Work item | Scope | Requirements | Kind |
|---|---|---|---|
| WI-0040 | Token retention: risk statement and no persistence by default now; sealed HttpOnly refresh cookie design for the next protocol version | FID-SES-008 | P0, high |
| WI-0036 | Applications adopt the default-deny decision vocabulary, classify permissions, store roles in administrators-only repositories, prevent self-elevation, bound revocation | FID-AUZ-003..007, FID-AUZ-011 | P0/P1, high |
| WI-0037 | Tenant model: organization key, derived membership, switching, trusted tenant context, repository set per tenant | FID-ORG-001..003, FID-ORG-007, FID-DATA-003..004 | P0/P1 |
| WI-0039 | Service identity contract | FID-SVC-001..004 | P1 |
| WI-0038 | (Deferred) organization administration, delegation, invitations | FID-ORG-004..006 | deferred |

Still waiting on the owner: the latency and availability targets
(FID-REL-001..002, WI-0033). Still waiting on the operator: a test GitHub
App (WI-0026) and a test AWS environment (FID-OPS-013).

