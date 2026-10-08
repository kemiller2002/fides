# Repository Semantic Map

This initial routing map was established by Conditor from accepted governing inputs. It does not infer application semantics.

## Governing inputs

- Canonical execution contract: `none declared`
- Accepted requirement artifacts:
- none declared

## Semantic areas

| Semantic area / feature | Purpose | Location | Manifest | Notes |
|---|---|---|---|---|
| Secret material | Typed, always-redacted tokens, codes, verifiers and client secrets (FID-EXC-005) | `src/Fides/Secrets.fs` | not established yet | Every secret prints as `<redacted>`; values leave only through `Secret.reveal`. |
| Effects | Provider HTTP, secret reads and the clock as data the host interprets (FID-EXC-002) | `src/Fides/Http.fs`, `src/Fides/Effect.fs` | not established yet | The core never performs I/O. |
| Provider model | Identity providers as records of pure request builders and response readers, with declared capabilities and a "proves" statement (FID-PRV-001, FID-PRV-004) | `src/Fides/Provider.fs` | not established yet | Adding a provider adds a catalog entry. |
| GitHub App provider | GitHub user-to-server tokens with expiry and refresh (FID-PRV-002, FID-PRV-003, DF-FIDES-2026-0003) | `src/Fides/GitHub.fs`, `src/Fides/Pkce.fs` | not established yet | Provider text never leaves the module; responses become typed refusals. |
| Configuration | The operator's registrations (exact origins and redirect URIs, required repository) and provider clients by secret reference, validated with every problem reported (FID-TB-003) | `src/Fides/Configuration.fs` | not established yet | Holds no secret. |
| Exchange protocol | The wire contract shared by exchange and client: requests, token response, refusal codes and statuses (EXCHANGE-PROTOCOL.md) | `src/Fides/Protocol.fs`, `src/Fides/JsonWrite.fs`, `src/Fides/JsonRead.fs` | not established yet | Reflection-free JSON so it trims and runs under WebAssembly. |
| Exchange core | Validation before any provider call, code exchange, identity, required-repository check, refresh and revocation; discards tokens it will not return (FID-EXC-001..005) | `src/Fides/Exchange.fs` | not established yet | Stateless (DF-FIDES-2026-0004). |
| Exchange service | The exchange as a host-neutral HTTP service: routing, body limits, CORS, cache headers, audit records without content (FID-HOST-001, FID-HOST-005) | `src/Fides/Service.fs` | not established yet | Hosting adapters translate to and from it. |
| Hosting runtime | Runs the core's effects with HttpClient (timeouts, no redirects, bounded responses), a cached secret port and the clock; audit log lines (FID-HOST-001) | `src/Fides.Hosting/` | not established yet | Cloud-neutral; every adapter builds on it. |
| AWS adapter | Lambda (provided.al2023, arm64) behind an API Gateway HTTP API: event translation, Secrets Manager port, bootstrap (FID-HOST-002) | `src/Fides.Hosting.Aws/`, `infrastructure/aws/`, `docs/hosting/AWS.md` | not established yet | Nothing is deployed from the repository (DF-FIDES-2026-0005). |
| WebAssembly client | Sign-in with PKCE and single-use state, callback, typed session states, retention modes, single-flight refresh, cross-tab messages, and the token provider mirroring Arca's port (FID-CLI-001..003) | `src/Fides.Client/`, `docs/client/README.md` | not established yet | Touches no browser API; the host supplies ports. Verified in browser-wasm by `tests/Fides.Client.Wasm`. |
| Acceptance and conformance | The acceptance scenarios, the GitHub simulator, the scenario runner and the platform-free adapter conformance suite (FID-TEST-001, FID-TEST-002) | `tests/Fides.Acceptance/`, `docs/hosting/ADAPTERS.md` | not established yet | Shared by the core's tests and every adapter's tests. |

## Repository-wide composition

- Composition/root entry point: `src/Fides/Fides.fsproj` (core); `src/Fides.Hosting.Aws/Program.fs` (AWS Lambda bootstrap)
- Shared contracts: see the governing inputs above.
- Architecture checks: installed Ordo/Praxis verification plus repository build/tests.
- Boundary checks: installed Ordo/Praxis verification and the declared Echelon foundations.

## Areas without separate manifests

No feature manifest is generated merely to satisfy structure. Create one when the initial mission establishes a real semantic ownership boundary.
