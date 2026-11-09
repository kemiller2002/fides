---
id: FIDES-SECURITY-ARCHITECTURE
title: Fides security architecture and trust boundaries
status: draft
version: 0.1.0
created: 2026-11-09
updated: 2026-11-09
owners:
  - fides
related_documents:
  - docs/architecture/TRUST-BOUNDARIES.md
  - docs/architecture/EXCHANGE-PROTOCOL.md
  - docs/architecture/AUTHORIZATION-OWNERSHIP.md
  - docs/security/THREAT-MODEL.md
  - docs/requirements/FIDES-REQUIREMENTS.md
tags: [security, architecture, trust-boundaries]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:49.923Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
    EXE-20261109T154841724Z-a435eb94:
      operations: [modified]
      at: 2026-11-09T15:50:42.961Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Record the owner's acceptance of DF-FIDES-2026-0010..0015 on 2026-11-09 (WI-0023)"
---

# Fides security architecture

Draft for owner review (WI-0022). It extends the accepted trust-boundary
model ([`TRUST-BOUNDARIES.md`](TRUST-BOUNDARIES.md), FID-TB-001..006)
without changing it, and states, for every security property, which
component is authoritative. DF-FIDES-2026-0010..0015 were accepted by the
owner on 2026-11-09.

## 1. Components and what each enforces

| Component | Runs in | Enforces | Must not be relied on for |
|---|---|---|---|
| Fides client (`EchelonFoundry.Fides.Client`) | Browser, F# WebAssembly | State and PKCE generation and checking; callback hygiene; session states; retention; single-flight refresh; cross-tab signals | Authorization of anything; protecting tokens from script in the origin |
| Fides exchange core (`EchelonFoundry.Fides`) | Pure F#, inside a host | Client registration, exact redirect URIs, origins, verifier format, no partial session, revoke-on-refusal, required-repository check at sign-in | Caller authentication (`Origin` can be forged by non-browser callers); data access; anything stateful |
| Fides hosting (`Fides.Hosting`, `Fides.Hosting.Aws`) | AWS Lambda behind API Gateway | Secret custody by reference, Aegis classification, audit lines without secrets, stage throttling | Per-caller rate limiting (missing, FID-OPS-006) |
| Fides Arca bridge (`EchelonFoundry.Fides.Arca`) | Browser | Hands Arca only the access token, with typed failures | Anything else |
| GitHub (provider) | github.com | Account authentication (password, 2FA, passkeys), App authorization, token issuance, rotation and revocation, **repository permissions** | Telling one Echelon application from another (one App's callback list is shared) |
| Arca | Browser | Uses the token; refuses on credential failure without mutation (ARCA-AUTH-004); one repository per permission boundary | Authorization: each application enforces its own capabilities (ARCA-AUTH-005) |
| Application | Browser (and, in future, a server-side point) | Its capability model; its list of protected operations and their enforcement points | Enforcing anything finer than a repository from the browser (DF-FIDES-2026-0013) |
| Limen | Browser kernel | Routing, return-to without open redirect, credential-parameter refusal | Authorization: guards are not an authorization boundary |
| Operator | AWS account, GitHub App settings | Secrets, App permissions and installations, environments | — |

## 2. Boundaries

B1-B5 are defined in TRUST-BOUNDARIES.md. This architecture adds:

- **B6 application ↔ application.** Several applications share the exchange
  and, today, one GitHub App per environment. Tokens are not bound to the
  application that obtained them (SEC-THR-107; FID-DATA-006, FID-AUZ-009).
- **B7 build and release.** Runners, actions, package feeds, the vendored
  Arca feed, echelon-registry (FID-OPS-009, FID-OPS-010).
- **B8 operator.** Holder of the client secret and App configuration
  (FID-OPS-012, FID-OPS-014).
- **B9 service ↔ GitHub** (DF-FIDES-2026-0014, accepted). A non-human
  principal with its own installation token, outside the browser exchange.

## 3. Authority for each permission check

| Check | Authoritative layer | Fides's role | Requirement |
|---|---|---|---|
| Is this a real GitHub account, and did its owner authorize the App? | GitHub | Reports the result | FID-PRV-003, FID-IDN-001 |
| May this application use Fides, from this origin, with this callback? | Fides exchange | Enforces | FID-TB-003, FID-HOST-005 |
| Is this callback the one this tab started? | Fides client | Enforces | FID-EXC-001, FID-IDN-004 |
| Can this token read or write repository R? | GitHub | Never re-implements; checks a required repository at sign-in and (planned) at refresh | FID-TB-004, FID-AUZ-008 |
| May this person do action X in application Y? | Application at its enforcement point | None (DF-FIDES-2026-0010, accepted) | FID-AUZ-001..005 |
| Which tenant is this request for? | Trusted configuration plus installation reach | None; tenant context must not come from the request | FID-DATA-003, FID-ORG-007 |
| May this refresh token be refreshed by application Y? | Fides exchange (planned) or provider (with per-application Apps) | Missing today | FID-AUZ-009 |
| May this service act on repository R? | GitHub, via the service's installation token | Defines the contract only | FID-SVC-001..004 |

## 4. Enforcement rules

1. **Default deny.** Every check above refuses when an input is unknown or
   unavailable. The exchange already does (typed refusals, no partial
   result); application authorization must (FID-AUZ-003).
2. **No UI authorization.** Hidden controls and route guards are
   presentation (FID-AUZ-002).
3. **Data authority is GitHub's.** With browser-held user tokens, the
   repository permission is the only enforced data boundary
   (FID-DATA-002, DF-FIDES-2026-0013).
4. **Tenant from trusted context only** (FID-DATA-003).
5. **No confused deputy.** The exchange uses its client credentials only
   for token, refresh and revocation calls, and the user's token for every
   other provider call (FID-DATA-008); it never touches application data.
6. **Fail closed on configuration.** Invalid configuration stops the host
   at start-up (FID-OPS-008).
7. **Secrets by reference.** The client secret is read from the secret
   store and cached for at most five minutes (FID-OPS-001).

## 5. Known violations of this architecture in the current code

| Finding | Requirement | Work |
|---|---|---|
| The exchange accepts any registered application's refresh or revocation of any token; provider clients are keyed by provider | FID-AUZ-009, FID-DATA-006 | WI-0025 |
| The required-repository check runs at sign-in only | FID-AUZ-008 | WI-0025 |
| The client compares exchange-clock expiries with the browser clock | FID-REL-006 | WI-0024 |
| A single 401 marks the session revoked | FID-SES-005 | WI-0024 |
| A failed re-sign-in sets `provider_unavailable` over a live session | FID-IDN-009 | WI-0024 |
| Audit lines carry no correlation id or principal | FID-OPS-004, FID-OPS-007 | WI-0030 |
| Persistent retention keeps a six-month refresh token in local storage | FID-SES-008 | WI-0023 (decision) |

## 6. Verification levels

Unit and simulated-provider tests prove Fides's own logic. The
emulated-host tests prove the deployed artefact's wiring. Browser-wasm runs
prove the client in the real runtime. **None of these proves a property of
real GitHub**; those are FID-TEST-010..013 and are blocked until the
operator registers a test GitHub App.
