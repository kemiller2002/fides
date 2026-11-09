# Fides known risks

| Risk | Likelihood | Impact | Mitigation | Owner |
|---|---|---|---|---|
| A token, code, client secret or PKCE verifier leaks into a log, error, URL or stored record | Medium | High | Opaque secret types with redacted `ToString`; fixed refusal messages; tests that scan every response and log for fixture secrets (FID-EXC-005) | Fides |
| An open redirect or callback substitution delivers a code to an attacker | Medium | High | Exact redirect-URI match per registered application; PKCE S256; single-use state (FID-TB-003, FID-EXC-001) | Fides |
| Refresh tokens held in the browser are stolen by injected script | Medium | High | Memory-only retention by default; persistent retention only by explicit user choice; GitHub App tokens expire in 8 hours (FID-CLI-002, DF-FIDES-2026-0004) | Fides |
| GitHub changes its OAuth or GitHub App token behaviour | Low | High | Provider responses are interpreted by one pure module with documented fixtures; provider outage is a typed state (FID-EXC-004) | Fides |
| Arca's token-provider port lands with a different shape from Fides's | Medium | Low | Fides exposes the documented shape (token or none/expired/revoked); a one-function adapter bridges any difference | Fides |
| An unexpected exception at the host leaks request content into diagnostics | Low | High | Aegis records the exception type only (message withheld); tests throw with secrets in the message and scan the Aegis events and log (DF-FIDES-2026-0009) | Fides |
| Documentation drifts from the product's purpose | Medium | High | Context gate test (FID-CTX-003) | Fides |
| Process overhead exceeds decision value | Medium | High | Measure time and rework; use artifact thresholds | Fides |
| A token issued to one application is refreshed or revoked through another application's registration; applications in an environment share one GitHub App's reach | High | High | FID-AUZ-009, FID-DATA-006 (WI-0025); DF-FIDES-2026-0013 | Fides |
| Application permissions finer than a repository are bypassed with the user's own GitHub token | High | High | Classify permissions; separate repositories per boundary; server-side point only by decision (FID-AUZ-004, DF-FIDES-2026-0013) | Owner, applications |
| Provider assumptions (single-use codes, PKCE, rotation, refresh grant death on revocation) hold only in Fides's simulator | Medium | High | Live-provider suite (FID-TEST-010..013, WI-0026), blocked on a test GitHub App | Fides, operator |
| Clock skew or a single 401 signs a person out as "revoked" | Medium | Medium | FID-REL-006, FID-SES-005 (WI-0024) | Fides |
| No tenant model, so cross-tenant isolation is unknown | Medium | High | DF-FIDES-2026-0011; FID-ORG-*, FID-TEST-006 | Owner |

