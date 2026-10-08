# Fides architecture

Fides is the common Echelon authentication and single sign-on (SSO) identity boundary.

## Components

```
 browser (Limen/Forma WASM app)           Fides exchange (stateless)         GitHub
 ┌──────────────────────────────┐        ┌───────────────────────────┐      ┌──────────────┐
 │ application                  │        │ hosting adapter (AWS      │      │ authorize    │
 │   └ Fides client ────────────┼─POST──▶│ Lambda + HTTP API)        │─────▶│ access_token │
 │       session, state, PKCE   │        │   └ pure F# exchange core │      │ GET /user    │
 │       token provider ──▶ Arca│        │ client secret (runtime    │      │ revoke       │
 └──────────────────────────────┘        │ secret store only)        │      └──────────────┘
                                         └───────────────────────────┘
```

- **Pure core (`src/Fides`).** Domain types, the provider model, the GitHub
  App provider and the exchange core. Every effect (HTTP to the provider,
  secret retrieval, clock, randomness) is an explicit request in the
  returned data; the core references no cloud SDK (FID-EXC-002,
  FID-HOST-001).
- **Hosting adapters.** Translate the host's request and response shapes
  and interpret the core's effect requests. AWS Lambda behind an HTTP API is
  the first (FID-HOST-002); Azure Functions remains possible (FID-HOST-004).
  Every adapter runs the same conformance suite (FID-TEST-002).
- **Client.** Runs in the browser as WebAssembly. Holds state and the PKCE
  verifier for one sign-in, the session tokens per the chosen retention mode,
  and exposes the token provider to Arca (FID-CLI-001..003).

## Boundaries that are costly to reverse

- The exchange holds no state: no session store, no server-held refresh
  tokens (DF-FIDES-2026-0004).
- The client secret exists only in the exchange's runtime secret store.
- Arca depends on a token-provider function shape, never on Fides.

## Architectural constraints

- Canonical records remain independent of any model vendor or chat.
- Tokens, codes, client secrets and PKCE verifiers never enter fixtures,
  logs, errors or prompts (FID-EXC-005).
- Generated views must not silently replace canonical source records.

The trust-boundary model is recorded by WI-0004.
