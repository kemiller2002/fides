---
id: FIDES-REQ
title: Fides single sign-on requirements
status: draft
version: 0.1.0
created: 2026-10-08
updated: 2026-10-08
owners:
  - fides
related_documents:
  - research/decisions/DF-FIDES-2026-0001--cloud-agnostic-exchange-aws-first.md
  - research/decisions/DF-FIDES-2026-0002--github-only-extensible-provider-model.md
tags: [requirements, authentication, sso, github, oauth]
provenance:
  contributions:
    EXE-20261008T082246199Z-61dcdd53:
      operations: [created]
      at: 2026-10-08T08:23:19.523Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Consolidated from issue #1, user decisions of 2026-10-08 and consumer sign-in requirements"
---

# Fides single sign-on requirements

Fides is the single sign-on (SSO) application that every Echelon application
shares. Before this document there was no requirements file in this
repository. These requirements are consolidated from:

| Source | What it contributes |
|---|---|
| Issue [#1](https://github.com/kemiller2002/fides/issues/1) "FIDES-P0 Correct repository semantic drift before implementing authentication" | Purpose, trust boundaries, the decision record on what GitHub proves, executable acceptance tests and the context gate (`FID-CTX`, `FID-TB`, `FID-TEST`) |
| User decisions of 2026-10-08 | Hosting (`FID-HOST`), the provider model (`FID-PRV`), Helix stays on its own sign-in |
| Consumer requirements | Chrona CHX-022, CHX-023, CHX-030; Summa SUM0-003, SUM0-004, SUM0-022, SUM0-023, SUM3-012, SUM3-023; Signal ADM-056, ADM-071, ADM-072; Arca ARCA-AUTH-001..005 (the token-provider port) |

Keywords follow RFC 2119.

## 1. Decisions

| ID | Decision | Record |
|---|---|---|
| FID-D-001 | Fides's server-side piece, the OAuth code-for-token exchange that holds the client secret, is cloud-agnostic: a pure core plus a thin hosting adapter. **AWS is the first target** (for example Lambda behind API Gateway). An Azure adapter must remain possible. | [DF-FIDES-2026-0001](../../research/decisions/DF-FIDES-2026-0001--cloud-agnostic-exchange-aws-first.md) |
| FID-D-002 | Start small: **GitHub is the only identity provider for now**, because Arca needs a GitHub token. The provider model is extensible so other providers can be added later. | [DF-FIDES-2026-0002](../../research/decisions/DF-FIDES-2026-0002--github-only-extensible-provider-model.md) |
| FID-D-003 | **Helix keeps its own sign-in for now** and does not move to Fides yet. Fides's first consumers are Chrona, then Summa, then Signal. | DF-FIDES-2026-0002 |
| FID-D-004 | Arca does not depend on Fides. Fides supplies a token provider that satisfies Arca's token-provider port (ARCA-AUTH-001). | DF-FIDES-2026-0002 |
| FID-D-005 | Build order: the minimal Fides slices (FID-CTX, FID-TB, FID-PRV-001..003, FID-EXC, FID-HOST-001..003, FID-CLI) come before Chrona's sign-in work. | user decision 2026-10-08 |

## 2. Context and identity of the repository (FID-CTX)

**FID-CTX-001** The charter MUST state Fides's purpose: the common Echelon
authentication/SSO identity boundary. It starts with GitHub identity and
GitHub repository access, without making GitHub the permanent
architecture. *Source: issue #1.*

**FID-CTX-002** `PROJECT-CHARTER.md`, `README.md`, `context/ARCHITECTURE.md`,
`context/CURRENT-STATE.md`, `context/DECISIONS.md` and the work queue MUST
agree on that purpose. *Source: issue #1.*

**FID-CTX-003** A repository-context test MUST fail if the charter or the
product identity reverts to unrelated boilerplate. *Source: issue #1.*

## 3. Trust boundaries (FID-TB)

**FID-TB-001** Fides MUST define its users (people signing in to Echelon
applications, and the applications themselves as OAuth clients) and its
trust boundaries: browser application, Fides exchange service, identity
provider and data repository. *Source: issue #1.*

**FID-TB-002** Fides MUST define token and session ownership: who holds the
provider access token, where it lives, for how long, and who may read it.
The client secret MUST only ever exist in the server-side exchange.
*Sources: issue #1; SUM0-004, SUM3-023, CHX-023.*

**FID-TB-003** Fides MUST define callback and redirect behaviour. Redirect
URIs MUST be registered per application and matched exactly; an
unregistered or modified callback MUST be refused. *Source: issue #1.*

**FID-TB-004** Fides MUST define the relationship between sign-in and
repository authorization. Authentication establishes who the user is.
GitHub's repository permissions decide what the token can reach. Each
application's own capability model decides what the user may do in it.
*Sources: issue #1; CHX-022, CHX-030, SUM0-003, SUM0-022.*

**FID-TB-005** Fides MUST define logout and revocation: sign-out clears the
token in every client store, and revoked or expired tokens are reported to
applications as typed states. *Sources: issue #1; SIG ADM-056, SIG ADM-072.*

**FID-TB-006** A decision record MUST state what GitHub authentication
proves (control of a GitHub account and the token's granted scopes) and
what Fides must own itself (session, client registration, redirect policy,
revocation propagation). *Source: issue #1.*

## 4. Provider model (FID-PRV)

**FID-PRV-001** Fides MUST model identity providers behind a provider
interface. The interface covers authorization URL construction, code
exchange, identity resolution and revocation, with each provider's
capabilities declared explicitly. *Source: FID-D-002.*

**FID-PRV-002** GitHub MUST be the only provider implemented for now. It
MUST support the scopes Arca needs for repository read/write, and it SHOULD
prefer the narrowest grant: a GitHub App user-to-server token, or
fine-grained repository access where available. *Sources: FID-D-002;
SIG ADM-004 ("prefer a fine-grained token").*

**FID-PRV-003** The resolved identity MUST come from the provider, for
example GitHub `GET /user`, never from a typed username. *Sources: CHX-022,
SUM0-003.*

**FID-PRV-004** Adding a second provider MUST NOT require changes to
consuming applications beyond configuration. *Source: FID-D-002.*

## 5. Code-for-token exchange (FID-EXC)

**FID-EXC-001** The exchange MUST use the OAuth 2.0 authorization-code flow
with PKCE and a single-use `state`. The pure core MUST validate state,
verifier, redirect URI and client registration before any provider call.
*Sources: issue #1 (invalid state/nonce, replay, wrong callback).*

**FID-EXC-002** The exchange core MUST be pure F#. It takes the request, the
configuration and the provider responses as data, and returns the response
or a typed refusal. Every effect (HTTP to the provider, secret retrieval,
clock, randomness) MUST be an explicit input or effect request. *Sources:
FID-D-001; user functional-style preference.*

**FID-EXC-003** Replayed codes and states MUST be refused. *Source: issue #1.*

**FID-EXC-004** A provider outage MUST produce a typed, user-presentable
failure and never a partial session. *Source: issue #1.*

**FID-EXC-005** Tokens, codes, client secrets and PKCE verifiers MUST NOT
appear in logs, telemetry, errors, URLs after the callback, or stored
application data. Unexpected failures MUST be classified through Aegis.
*Sources: CHX-023, SUM0-004, SIG ADM-071; shared foundations (Aegis).*

## 6. Hosting (FID-HOST)

**FID-HOST-001** The exchange MUST be deployable through a thin hosting
adapter that translates the host's request and response shapes into the pure
core's types and back. The core MUST NOT reference any cloud SDK. *Source:
FID-D-001.*

**FID-HOST-002** The first adapter MUST target AWS: Lambda behind API Gateway
(HTTP API), with the client secret in AWS Secrets Manager or SSM Parameter
Store, read at runtime. *Source: FID-D-001.*

**FID-HOST-003** Deployment MUST be reproducible from the repository
(infrastructure as code) with separate environments (test, staging,
production) and separate OAuth app registrations per environment. *Sources:
FID-D-001; SUM0-038.*

**FID-HOST-004** An Azure adapter (Azure Functions plus Key Vault) MUST
remain possible without touching the core. A shared adapter conformance
suite proves that both adapters behave identically. Building the Azure
adapter is deferred. *Source: FID-D-001.*

**FID-HOST-005** CORS MUST allow exactly the registered application
origins. *Source: FID-TB-003.*

## 7. Client library for applications (FID-CLI)

**FID-CLI-001** Fides MUST ship a client library usable from Limen/Forma
F# WASM applications. The library starts sign-in, completes the callback,
holds the session and exposes a **token provider** that satisfies Arca's
token-provider port, without Arca referencing Fides. *Sources: FID-D-004;
ARCA-AUTH-001.*

**FID-CLI-002** The client MUST support explicit token-retention modes:
memory-only (the default), session-scoped, and persistent only by explicit
user choice with clear disclosure and a clear-token action. *Sources:
SIG ADM-071, CHX-023 ("session-only storage should be supported").*

**FID-CLI-003** The client MUST expose typed session states (signed out,
signing in, signed in, expired, revoked, provider unavailable) and SHOULD
signal changes across tabs. Applications MUST still rely on provider checks
rather than on the cross-tab signal. *Sources: SIG ADM-056, SIG ADM-072.*

**FID-CLI-004** The client MUST be published as a pinned, versioned package
through the Echelon release contract. *Source: shared foundations
(dependency pinning).*

## 8. Executable acceptance tests (FID-TEST)

**FID-TEST-001** Before any production implementation, executable acceptance
tests MUST exist for: invalid state/nonce, replay, wrong callback, revoked
identity, expired session, repository access denial and provider outage.
*Source: issue #1.*

**FID-TEST-002** The hosting adapters MUST pass a shared adapter conformance
suite. *Source: FID-HOST-004.*

## 9. Open questions for the user

| ID | Question |
|---|---|
| OQ-FIDES-001 | GitHub OAuth App or GitHub App (user-to-server tokens, fine-grained per-repository access, expiring tokens with refresh)? The GitHub App is the narrower grant and fits Arca's "separate repository per permission boundary" decision better. Fides needs the choice before FID-PRV-002. |
| OQ-FIDES-002 | Should Fides hold refresh tokens server-side (a session service) or stay stateless, returning the token to the browser client only? Stateless is simpler and cheaper on Lambda. A session service allows central revocation. |
| OQ-FIDES-003 | Which AWS account and region, and which domain name will the exchange endpoint use? |
