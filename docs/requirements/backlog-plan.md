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
