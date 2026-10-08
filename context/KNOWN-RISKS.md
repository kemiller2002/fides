# Fides known risks

| Risk | Likelihood | Impact | Mitigation | Owner |
|---|---|---|---|---|
| A token, code, client secret or PKCE verifier leaks into a log, error, URL or stored record | Medium | High | Opaque secret types with redacted `ToString`; fixed refusal messages; tests that scan every response and log for fixture secrets (FID-EXC-005) | Fides |
| An open redirect or callback substitution delivers a code to an attacker | Medium | High | Exact redirect-URI match per registered application; PKCE S256; single-use state (FID-TB-003, FID-EXC-001) | Fides |
| Refresh tokens held in the browser are stolen by injected script | Medium | High | Memory-only retention by default; persistent retention only by explicit user choice; GitHub App tokens expire in 8 hours (FID-CLI-002, DF-FIDES-2026-0004) | Fides |
| GitHub changes its OAuth or GitHub App token behaviour | Low | High | Provider responses are interpreted by one pure module with documented fixtures; provider outage is a typed state (FID-EXC-004) | Fides |
| Arca's token-provider port lands with a different shape from Fides's | Medium | Low | Fides exposes the documented shape (token or none/expired/revoked); a one-function adapter bridges any difference | Fides |
| Aegis is not yet published, so failure classification cannot bind to it | High | Medium | Typed failure model now; binding tracked as its own work item | Fides |
| Documentation drifts from the product's purpose | Medium | High | Context gate test (FID-CTX-003) | Fides |
| Process overhead exceeds decision value | Medium | High | Measure time and rework; use artifact thresholds | Fides |
