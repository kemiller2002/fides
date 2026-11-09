---
id: FIDES-REQ
title: Fides single sign-on requirements
status: draft
version: 0.3.0
created: 2026-10-08
updated: 2026-11-09
owners:
  - fides
related_documents:
  - docs/requirements/README.md
  - docs/requirements/TRACEABILITY-MATRIX.md
  - docs/requirements/VERIFICATION-PLAN.md
  - docs/requirements/ORGANIZATION-AND-TENANT-ISOLATION.md
  - docs/requirements/UI-REQUIREMENTS-AND-SCREENS.md
  - docs/requirements/READINESS-AND-GAP-REPORT.md
  - docs/requirements/backlog-plan.md
  - docs/architecture/SECURITY-ARCHITECTURE.md
  - docs/architecture/AUTHORIZATION-OWNERSHIP.md
  - docs/security/THREAT-MODEL.md
  - research/decisions/DF-FIDES-2026-0001--cloud-agnostic-exchange-aws-first.md
  - research/decisions/DF-FIDES-2026-0002--github-only-extensible-provider-model.md
  - research/decisions/DF-FIDES-2026-0003--github-app-user-to-server-tokens.md
  - research/decisions/DF-FIDES-2026-0004--stateless-exchange.md
  - research/decisions/DF-FIDES-2026-0005--deployment-is-operator-configuration.md
  - research/decisions/DF-FIDES-2026-0006--interim-distribution-attested-github-releases.md
  - research/decisions/DF-FIDES-2026-0007--what-github-proves-and-what-fides-owns.md
  - research/decisions/DF-FIDES-2026-0008--client-mirrors-arca-token-provider-port.md
  - research/decisions/DF-FIDES-2026-0009--aegis-at-the-exchange-host.md
  - research/decisions/DF-FIDES-2026-0010--authorization-ownership.md
  - research/decisions/DF-FIDES-2026-0011--organization-membership-ownership.md
  - research/decisions/DF-FIDES-2026-0012--stateless-exchange-replay-and-revocation-limits.md
  - research/decisions/DF-FIDES-2026-0013--github-backed-storage-permission-and-tenancy.md
  - research/decisions/DF-FIDES-2026-0014--service-identities.md
  - research/decisions/DF-FIDES-2026-0015--browser-token-retention.md
tags: [requirements, authentication, authorization, sso, github, oauth, security]
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
    EXE-20261008T084302418Z-58dc4016:
      operations: [modified]
      at: 2026-10-08T08:46:19.913Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Resolve OQ-FIDES-001..003 with the user's decisions (GitHub App, stateless exchange, deployment as configuration)"
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [modified]
      at: 2026-11-09T14:44:53.199Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Requirements baseline 0.3.0: record format, revised FID-TB-004/EXC-003/HOST-003/CLI-002, 101 added requirements (WI-0022)"
---

# Fides single sign-on requirements

Fides is the single sign-on (SSO) application that every Echelon application
shares. This is the canonical requirements catalogue. Version 0.3.0
(WI-0022) keeps every requirement of 0.2.0 under its original ID, revises
five of them, and adds families for identity, sessions, non-human
identities, authorization, data-layer security, multiple organizations,
application integration and user experience, operational security, and
reliability.

Keywords follow RFC 2119. Read [`README.md`](README.md) for the index and
how to change this file.

## How to read a requirement

Every requirement is a record: `**FID-FAMILY-NNN**` and its normative
statement, then these fields. `tests/Fides.Tests/RequirementsCatalogueTests.fs`
fails the build if a field is missing or a value is not allowed, and checks
the status against the repository:

| Field | Meaning |
|---|---|
| Priority | `P0` blocks the next consumer or is security-critical; `P1` needed before more than one consumer relies on Fides in production; `P2` should; `P3` future capability |
| Owner | The component that holds the enforcement point. An owner outside Fides means Fides does not enforce it and must not claim to |
| Status | `implemented-verified` (a test carries `[<Trait("Verifies", ID)>]` at every required level), `implemented-unverified`, `missing`, `blocked` (waits on a decision or an external party), `deferred` |
| Change | `retained`, `revised` or `added` in 0.3.0 |
| Source | Where the requirement comes from |
| Rationale | Why it exists |
| Depends on | Requirements it needs first |
| Acceptance | What must be true to call it done |
| Positive test | The test that shows the allowed path works |
| Negative test | The adversarial or refusal test; `n/a: reason` only where nothing can be attacked |
| Evidence | Implementation and test locations, or `none` |
| Levels achieved | `document`, `unit`, `integration`, `simulated-provider`, `emulated-host`, `browser-wasm`, `live-provider`, `manual-accessibility`, or `none` |
| Levels required | The levels at which the property must be shown before it may be called verified. A property of real GitHub needs `live-provider`; simulation never satisfies it |

A requirement that is not `implemented-verified` must be named by an open
work item (`./praxis work ready`; `docs/requirements/backlog-plan.md`).

## 1. Decisions

| ID | Decision | Record |
|---|---|---|
| FID-D-001 | Fides's server-side piece, the OAuth code-for-token exchange that holds the client secret, is cloud-agnostic: a pure core plus a thin hosting adapter. **AWS is the first target**. An Azure adapter must remain possible. | DF-FIDES-2026-0001 (accepted) |
| FID-D-002 | **GitHub is the only identity provider for now**, behind an extensible provider model. | DF-FIDES-2026-0002 (accepted) |
| FID-D-003 | **Helix keeps its own sign-in for now.** Fides's first consumers are Chrona, then Summa, then Signal. | DF-FIDES-2026-0002 (accepted) |
| FID-D-004 | Arca does not depend on Fides. Fides supplies a token provider that satisfies Arca's token-provider port (ARCA-AUTH-001). | DF-FIDES-2026-0002 (accepted) |
| FID-D-005 | Build order: the minimal slices came before Chrona's sign-in work. *Historical: the slices are complete (WI-0003..WI-0010).* | user decision 2026-10-08 |
| FID-D-006 | The GitHub provider is a **GitHub App** with expiring user-to-server tokens and refresh. | DF-FIDES-2026-0003 (accepted) |
| FID-D-007 | The exchange is **stateless**: no session service, no server-held refresh tokens. | DF-FIDES-2026-0004 (accepted) |
| FID-D-008 | The AWS account, region and domain are **operator configuration**; nothing is deployed from this work. | DF-FIDES-2026-0005 (accepted) |
| FID-D-009 | Interim distribution: **attested GitHub release assets plus an echelon-registry entry**. | DF-FIDES-2026-0006 (accepted) |
| FID-D-010 | Fides stays **authentication-only**; authorization is application-owned against a shared vocabulary; no central authorization service. | DF-FIDES-2026-0010 (**proposed, needs owner approval**) |
| FID-D-011 | Organization membership is **not held by Fides**; tenant boundaries map to GitHub App installations and Arca repositories. | DF-FIDES-2026-0011 (**proposed**) |
| FID-D-012 | The stateless exchange is kept; its replay and revocation limits are stated and accepted, with the conditions that would supersede DF-FIDES-2026-0004. | DF-FIDES-2026-0012 (**proposed**) |
| FID-D-013 | With browser-held user tokens, **GitHub repository permission is the only enforced data boundary**; one GitHub App per application data-isolation domain. | DF-FIDES-2026-0013 (**proposed**) |
| FID-D-014 | Non-human identities use **GitHub App installation tokens minted in their own runtime**, never a person's token and never the browser-facing exchange. | DF-FIDES-2026-0014 (**proposed**) |
| FID-D-015 | Memory-only retention stays the default; persistent retention of the refresh token is **restricted pending a sealed-cookie design**; applications must ship a strict CSP. | DF-FIDES-2026-0015 (**proposed**) |

Proposed decisions do not supersede accepted ones. Requirements that depend
on a proposed decision are `blocked` until the owner decides.

## 2. Context and identity of the repository (FID-CTX)

**FID-CTX-001** The charter MUST state Fides's purpose: the common Echelon authentication/SSO identity boundary. It starts with GitHub identity and GitHub repository access, without making GitHub the permanent architecture.

- Priority: P1
- Owner: Fides.Governance
- Status: implemented-verified
- Change: retained
- Source: issue #1
- Rationale: The scaffold's boilerplate described a different product; agents acted on it.
- Depends on: none
- Acceptance: PROJECT-CHARTER.md contains the purpose statement verbatim.
- Positive test: RepositoryContextTests: every context file states Fides's purpose.
- Negative test: RepositoryContextTests: a charter reverted to the scaffold boilerplate fails the gate.
- Evidence: PROJECT-CHARTER.md; tests/Fides.Tests/RepositoryContextTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-CTX-002** `PROJECT-CHARTER.md`, `README.md`, `context/ARCHITECTURE.md`, `context/CURRENT-STATE.md`, `context/DECISIONS.md` and the work queue MUST agree on that purpose.

- Priority: P1
- Owner: Fides.Governance
- Status: implemented-verified
- Change: retained
- Source: issue #1
- Rationale: A successor reads several files; all must say the same thing.
- Depends on: FID-CTX-001
- Acceptance: Each context file contains the purpose statement and no scaffold marker.
- Positive test: RepositoryContextTests: every context file states Fides's purpose.
- Negative test: RepositoryContextTests: boilerplate in the work queue fails the gate.
- Evidence: tests/Fides.Tests/RepositoryContext.fs; tests/Fides.Tests/RepositoryContextTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-CTX-003** A repository-context test MUST fail if the charter or the product identity reverts to unrelated boilerplate.

- Priority: P1
- Owner: Fides.Governance
- Status: implemented-verified
- Change: retained
- Source: issue #1
- Rationale: Drift must be caught mechanically.
- Depends on: FID-CTX-002
- Acceptance: The gate test reports each reintroduced marker.
- Positive test: RepositoryContextTests on the current repository.
- Negative test: RepositoryContextTests: a context file that keeps the purpose but reintroduces boilerplate fails.
- Evidence: tests/Fides.Tests/RepositoryContextTests.fs
- Levels achieved: unit
- Levels required: unit

## 3. Trust boundaries (FID-TB)

**FID-TB-001** Fides MUST define its users (people signing in to Echelon applications, and the applications themselves as OAuth clients) and its trust boundaries: browser application, Fides exchange service, identity provider and data repository.

- Priority: P0
- Owner: Fides.Governance
- Status: implemented-verified
- Change: retained
- Source: issue #1
- Rationale: Controls are meaningless without stated boundaries.
- Depends on: none
- Acceptance: TRUST-BOUNDARIES.md section 1 and 2; SECURITY-ARCHITECTURE.md extends them with B6-B8.
- Positive test: TrustBoundaryModelTests: a section defines each FID-TB requirement.
- Negative test: TrustBoundaryModelTests: a model missing a section is detected.
- Evidence: docs/architecture/TRUST-BOUNDARIES.md; docs/architecture/SECURITY-ARCHITECTURE.md; tests/Fides.Tests/TrustBoundaryModelTests.fs
- Levels achieved: document, unit
- Levels required: document, unit

**FID-TB-002** Fides MUST define token and session ownership: who holds the provider access token, where it lives, for how long, and who may read it. The client secret MUST only ever exist in the server-side exchange.

- Priority: P0
- Owner: Fides.Governance
- Status: implemented-verified
- Change: retained
- Source: issue #1; SUM0-004, SUM3-023, CHX-023
- Rationale: Token custody is the core security property of an SSO boundary.
- Depends on: FID-TB-001
- Acceptance: The ownership table names every material; the secret appears only in the exchange.
- Positive test: TrustBoundaryModelTests: the model names every material that crosses a boundary.
- Negative test: SecretsTests and FunctionTests leak scans find no secret outside the exchange.
- Evidence: docs/architecture/TRUST-BOUNDARIES.md section 3; tests/Fides.Tests/SecretsTests.fs
- Levels achieved: document, unit
- Levels required: document, unit

**FID-TB-003** Fides MUST define callback and redirect behaviour. Redirect URIs MUST be registered per application and matched exactly; an unregistered or modified callback MUST be refused.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-verified
- Change: retained
- Source: issue #1
- Rationale: Callback substitution delivers codes to attackers (SEC-THR-103).
- Depends on: FID-TB-001
- Acceptance: Ordinal equality, no normalisation; refusal before any provider call.
- Positive test: ExchangeSecurityTests: redirect URIs are allow-listed by exact ordinal equality (allowed cases).
- Negative test: ExchangeSecurityTests (trailing slash, case, query, prefix variants); acceptance `wrong-callback`.
- Evidence: src/Fides/Exchange.fs; src/Fides/Configuration.fs; tests/Fides.Tests/ExchangeSecurityTests.fs; tests/Fides.Tests/ConfigurationTests.fs
- Levels achieved: document, unit, simulated-provider
- Levels required: unit, simulated-provider

**FID-TB-004** Fides MUST define the relationship between sign-in and authorization. Authentication establishes who the user is. GitHub's repository permissions decide what the token can reach. Each application's own capability model decides what the user may do in it, under the ownership rules of FID-AUZ-001..003.

- Priority: P0
- Owner: Fides.Governance
- Status: implemented-verified
- Change: revised
- Source: issue #1; CHX-022, CHX-030, SUM0-003, SUM0-022; ARCA-AUTH-005
- Rationale: Revised to point at the authorization family and the ownership proposal; the definition itself is unchanged.
- Depends on: FID-TB-001
- Acceptance: TRUST-BOUNDARIES.md section 5 and AUTHORIZATION-OWNERSHIP.md state the three layers and their owners.
- Positive test: TrustBoundaryModelTests: a section defines FID-TB-004; acceptance `repository-access-denied`.
- Negative test: ExchangeAcceptanceTests `repository-access-denied` (the token is revoked and not returned).
- Evidence: docs/architecture/TRUST-BOUNDARIES.md section 5; docs/architecture/AUTHORIZATION-OWNERSHIP.md
- Levels achieved: document, unit, simulated-provider
- Levels required: document, unit

**FID-TB-005** Fides MUST define logout and revocation: sign-out clears the token in every client store, and revoked or expired tokens are reported to applications as typed states.

- Priority: P0
- Owner: Fides.Client
- Status: implemented-verified
- Change: retained
- Source: issue #1; SIG ADM-056, SIG ADM-072
- Rationale: Sign-out must not leave usable tokens in the browser.
- Depends on: FID-TB-002
- Acceptance: Sign-out clears memory, session and local stores before the network call; states are typed.
- Positive test: ClientAcceptanceTests `sign-out-clears`.
- Negative test: ClientTests: sign-out clears locally even when revocation fails, and says so.
- Evidence: src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs; tests/Fides.Tests/Acceptance/ClientAcceptanceTests.fs
- Levels achieved: document, unit, simulated-provider
- Levels required: unit, simulated-provider

**FID-TB-006** A decision record MUST state what GitHub authentication proves (control of a GitHub account and the token's granted reach) and what Fides must own itself (session, client registration, redirect policy, revocation propagation).

- Priority: P0
- Owner: Fides.Governance
- Status: implemented-verified
- Change: retained
- Source: issue #1
- Rationale: Trusting the provider for what it does not prove is a defect.
- Depends on: FID-TB-001
- Acceptance: DF-FIDES-2026-0007 is accepted and states both sides.
- Positive test: TrustBoundaryModelTests: the GitHub-proves decision is accepted and states both sides.
- Negative test: ExchangeAcceptanceTests `wrong-callback`: a callback GitHub's shared App callback list would accept is still refused by Fides's own per-application redirect policy, the clearest case of something GitHub does not prove.
- Evidence: research/decisions/DF-FIDES-2026-0007--what-github-proves-and-what-fides-owns.md
- Levels achieved: document, unit
- Levels required: document

## 4. Provider model (FID-PRV)

**FID-PRV-001** Fides MUST model identity providers behind a provider interface covering authorization URL construction, code exchange, identity resolution and revocation, with each provider's capabilities and its "proves" statement declared explicitly.

- Priority: P1
- Owner: Fides.Core
- Status: implemented-verified
- Change: retained
- Source: FID-D-002
- Rationale: A second provider must be a Fides-only change.
- Depends on: FID-TB-006
- Acceptance: IdentityProvider declares capabilities and proves text.
- Positive test: ProviderTests: the GitHub provider declares its capabilities and what it proves.
- Negative test: ProviderCatalogTests: an unconfigured provider is absent, not defaulted.
- Evidence: src/Fides/Provider.fs; tests/Fides.Tests/ProviderTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-PRV-002** GitHub MUST be the only provider implemented for now, as a GitHub App issuing expiring user-to-server tokens with refresh tokens. A token response without expiry MUST be a provider contract violation.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-verified
- Change: retained
- Source: FID-D-002, FID-D-006; SIG ADM-004
- Rationale: Non-expiring tokens make theft permanent.
- Depends on: FID-PRV-001
- Acceptance: Fides parses 8-hour and 6-month lifetimes and refuses responses without them. GitHub's real lifetimes are confirmed live under FID-TEST-010.
- Positive test: ProviderTests: a GitHub App code exchange yields an 8-hour access token and a 6-month refresh token.
- Negative test: ProviderTests: token responses are interpreted into typed refusals (missing expiry).
- Evidence: src/Fides/GitHub.fs; tests/Fides.Tests/ProviderTests.fs
- Levels achieved: unit, simulated-provider
- Levels required: unit, simulated-provider

**FID-PRV-003** The resolved identity MUST come from the provider (GitHub `GET /user` for the token), never from a typed username.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-verified
- Change: retained
- Source: CHX-022, SUM0-003; ARCA-AUTH-003
- Rationale: A typed username is an assertion, not an identity.
- Depends on: FID-PRV-001
- Acceptance: Identity is read only from the provider response for the issued token.
- Positive test: ProviderTests: the identity comes from GitHub's user endpoint for the token.
- Negative test: ExchangeAcceptanceTests `revoked-identity` (identity refusal yields no session).
- Evidence: src/Fides/GitHub.fs; tests/Fides.Tests/ProviderTests.fs; tests/Fides.Tests/ClientEndToEndTests.fs
- Levels achieved: unit, simulated-provider, browser-wasm
- Levels required: unit, simulated-provider

**FID-PRV-004** Adding a second provider MUST NOT require changes to consuming applications beyond configuration.

- Priority: P2
- Owner: Fides.Core
- Status: implemented-verified
- Change: retained
- Source: FID-D-002
- Rationale: Applications should not be rewritten for a provider.
- Depends on: FID-PRV-001
- Acceptance: The client chooses its provider by id from configuration.
- Positive test: ProviderCatalogTests: switching provider is a configuration change.
- Negative test: ProviderCatalogTests: an unconfigured provider is absent, not defaulted; ClientTests: an unconfigured provider cannot start a sign-in.
- Evidence: src/Fides/Provider.fs; tests/Fides.Tests/ProviderCatalogTests.fs
- Levels achieved: unit
- Levels required: unit

## 5. Code-for-token exchange (FID-EXC)

**FID-EXC-001** The exchange MUST use the OAuth 2.0 authorization-code flow with PKCE (S256) and a single-use `state`. The pure core MUST validate state (client), verifier, redirect URI, origin and client registration before any provider call.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-verified
- Change: retained
- Source: issue #1 (invalid state, replay, wrong callback); RFC 7636; RFC 9700
- Rationale: Login CSRF and code injection (SEC-THR-101, SEC-THR-102).
- Depends on: FID-TB-003
- Acceptance: Every refusal that does not need the provider happens with no provider call.
- Positive test: ClientTests: sign-in sends the person to GitHub with a fresh state and an S256 challenge.
- Negative test: ClientAcceptanceTests `invalid-state`, `missing-state`, `unsolicited-callback`, `expired-state`; PkceTests.
- Evidence: src/Fides/Exchange.fs; src/Fides/Pkce.fs; src/Fides.Client/SignIn.fs; tests/Fides.Tests/PkceTests.fs; tests/Fides.Tests/ExchangeSecurityTests.fs
- Levels achieved: unit, simulated-provider, browser-wasm
- Levels required: unit, simulated-provider

**FID-EXC-002** The exchange core MUST be pure F#: request, configuration and provider responses in as data; a response or a typed refusal out. Every effect (HTTP, secret retrieval, clock, randomness) MUST be an explicit effect request.

- Priority: P1
- Owner: Fides.Core
- Status: implemented-verified
- Change: retained
- Source: FID-D-001; functional-style preference
- Rationale: A pure core is testable without a cloud and portable across hosts.
- Depends on: none
- Acceptance: The core references no cloud SDK and performs no I/O.
- Positive test: CoreBoundaryTests: the core's references are the base library, FSharp.Core and System.Text.Json.
- Negative test: CoreBoundaryTests: the core references no cloud SDK, HTTP client or file system.
- Evidence: src/Fides/Effect.fs; tests/Fides.Tests/CoreBoundaryTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-EXC-003** Replayed codes and states MUST be refused. State replay is refused by the client; code replay is refused by the provider and reported as `code_rejected`.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-verified
- Change: revised
- Source: issue #1
- Rationale: Revised to say which party enforces which replay refusal. Fides's handling is verified against the simulator; GitHub's own single-use enforcement is FID-TEST-010.
- Depends on: FID-EXC-001
- Acceptance: A second callback with the same state is `state_invalid` without an exchange call; a replayed code is `code_rejected`.
- Positive test: ClientAcceptanceTests `callback-succeeds`.
- Negative test: ClientAcceptanceTests `replayed-state`; ExchangeAcceptanceTests `replayed-code`.
- Evidence: src/Fides.Client/FidesClient.fs; tests/Fides.Tests/Acceptance/ClientAcceptanceTests.fs; tests/Fides.Tests/Acceptance/ExchangeAcceptanceTests.fs
- Levels achieved: unit, simulated-provider
- Levels required: unit, simulated-provider

**FID-EXC-004** A provider outage MUST produce a typed, user-presentable failure and never a partial session.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-verified
- Change: retained
- Source: issue #1
- Rationale: A partial session can grant access without identity.
- Depends on: FID-EXC-001
- Acceptance: Outage at any step yields `provider_unavailable` and any issued token is revoked.
- Positive test: ExchangeAcceptanceTests on a healthy provider.
- Negative test: ExchangeAcceptanceTests `provider-outage`; ClientAcceptanceTests `callback-provider-outage`; FunctionTests.
- Evidence: src/Fides/Exchange.fs; tests/Fides.Tests/Acceptance/ExchangeAcceptanceTests.fs; tests/Fides.Hosting.Aws.Tests/FunctionTests.fs
- Levels achieved: unit, simulated-provider, emulated-host
- Levels required: unit, simulated-provider

**FID-EXC-005** Tokens, codes, client secrets and PKCE verifiers MUST NOT appear in logs, telemetry, errors, URLs after the callback, or stored application data. Unexpected failures MUST be classified through Aegis.

- Priority: P0
- Owner: Fides.Hosting
- Status: implemented-verified
- Change: retained
- Source: CHX-023, SUM0-004, SIG ADM-071; Aegis
- Rationale: Logs and errors are the most common credential leak (SEC-THR-113).
- Depends on: FID-TB-002
- Acceptance: Leak scans over every response, audit line and Aegis event find no fixture secret.
- Positive test: DiagnosticsTests: a successful sign-in and an ordinary refusal report nothing to Aegis.
- Negative test: DiagnosticsTests: an exception escaping the exchange is an Aegis fault and an internal_error that discloses nothing; ExchangeSecurityTests: every refusal body is exactly the code.
- Evidence: src/Fides/Secrets.fs; src/Fides.Hosting/Diagnostics.fs; tests/Fides.Hosting.Aws.Tests/DiagnosticsTests.fs; tests/Fides.Tests/SecretsTests.fs
- Levels achieved: unit, simulated-provider, emulated-host
- Levels required: unit, emulated-host

## 6. Hosting (FID-HOST)

**FID-HOST-001** The exchange MUST be deployable through a thin hosting adapter that translates the host's request and response shapes into the pure core's types and back. The core MUST NOT reference any cloud SDK.

- Priority: P1
- Owner: Fides.Hosting
- Status: implemented-verified
- Change: retained
- Source: FID-D-001
- Rationale: Portability and testability.
- Depends on: FID-EXC-002
- Acceptance: Translation round-trips; the core has no cloud reference.
- Positive test: TranslationTests.
- Negative test: CoreBoundaryTests: the core references no cloud SDK, HTTP client or file system.
- Evidence: src/Fides/Service.fs; src/Fides.Hosting.Aws/Translation.fs; tests/Fides.Hosting.Aws.Tests/TranslationTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-HOST-002** The first adapter MUST target AWS: Lambda behind API Gateway (HTTP API), with the client secret in AWS Secrets Manager or SSM Parameter Store, read at runtime.

- Priority: P1
- Owner: Fides.Hosting.Aws
- Status: implemented-verified
- Change: retained
- Source: FID-D-001
- Rationale: The operator's first cloud.
- Depends on: FID-HOST-001
- Acceptance: The built bootstrap runs against an emulated Lambda Runtime API and reads the secret by reference.
- Positive test: RuntimeEmulationTests; SecretsTests.
- Negative test: SecretsTests: a Secrets Manager failure is unavailable and does not throw; DiagnosticsTests: an unreadable client secret is reported to Aegis as a configuration fault.
- Evidence: src/Fides.Hosting.Aws; tests/Fides.Hosting.Aws.Tests/RuntimeEmulationTests.fs; tests/Fides.Hosting.Aws.Tests/SecretsTests.fs
- Levels achieved: unit, emulated-host
- Levels required: unit, emulated-host

**FID-HOST-003** The infrastructure template MUST make deployment reproducible from the repository with separate environments (test, staging, production) and separate GitHub App registrations per environment. Verifying a real deployment is FID-OPS-013.

- Priority: P1
- Owner: Fides.Hosting.Aws
- Status: implemented-verified
- Change: revised
- Source: FID-D-001; SUM0-038
- Rationale: Revised to separate the template property (verified) from a real deployment (never performed; DF-FIDES-2026-0005).
- Depends on: FID-HOST-002
- Acceptance: Every environment-specific value is a template parameter; no secret or account is hard-coded.
- Positive test: TemplateTests: one stack serves one named environment with its own GitHub App.
- Negative test: TemplateTests: the template hard-codes no account, region, ARN or secret.
- Evidence: infrastructure/aws/fides-exchange.template.json; tests/Fides.Hosting.Aws.Tests/TemplateTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-HOST-004** An Azure adapter MUST remain possible without touching the core. A shared adapter conformance suite proves that adapters behave identically. Building the Azure adapter is deferred.

- Priority: P3
- Owner: Fides.Hosting
- Status: implemented-verified
- Change: retained
- Source: FID-D-001
- Rationale: Second cloud without a rewrite.
- Depends on: FID-HOST-001
- Acceptance: The conformance suite runs against any adapter; the AWS adapter passes. (The Azure adapter itself is WI-0011.)
- Positive test: ConformanceTests (AWS adapter).
- Negative test: ConformanceSuiteTests: an adapter that drops CORS headers does not conform.
- Evidence: tests/Fides.Acceptance/Conformance.fs; tests/Fides.Hosting.Aws.Tests/ConformanceTests.fs
- Levels achieved: unit, emulated-host
- Levels required: unit

**FID-HOST-005** CORS MUST allow exactly the registered application origins, never `*`, never credentials.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-verified
- Change: retained
- Source: FID-TB-003
- Rationale: Browsers must not let other sites call the exchange with a victim's material. CORS is not authentication (FID-OPS-005).
- Depends on: FID-TB-003
- Acceptance: Registered origin echoed; unregistered origin gets no CORS headers and `origin_not_allowed`.
- Positive test: ExchangeSecurityTests: a preflight from a registered origin gets that origin back.
- Negative test: ExchangeSecurityTests: another application's origin gets neither the token nor CORS headers.
- Evidence: src/Fides/Service.fs; tests/Fides.Tests/ExchangeSecurityTests.fs; tests/Fides.Hosting.Aws.Tests/FunctionTests.fs
- Levels achieved: unit, simulated-provider, emulated-host
- Levels required: unit, emulated-host

## 7. Client library for applications (FID-CLI)

**FID-CLI-001** Fides MUST ship a client library usable from Limen/Forma F# WASM applications. The library starts sign-in, completes the callback, holds the session and exposes a token provider that satisfies Arca's token-provider port, without Arca referencing Fides.

- Priority: P0
- Owner: Fides.Client
- Status: implemented-verified
- Change: retained
- Source: FID-D-004; ARCA-AUTH-001
- Rationale: Every application needs the same sign-in.
- Depends on: FID-EXC-001
- Acceptance: The full flow runs in the trimmed browser-wasm runtime; the bridge maps to Arca's real types.
- Positive test: ClientEndToEndTests; ArcaBridgeTests; scripts/verify-wasm.sh.
- Negative test: ClientAcceptanceTests refusal scenarios.
- Evidence: src/Fides.Client; src/Fides.Arca/TokenBridge.fs; tests/Fides.Client.Wasm; tests/Fides.Tests/ArcaBridgeTests.fs
- Levels achieved: unit, simulated-provider, browser-wasm
- Levels required: unit, browser-wasm

**FID-CLI-002** The client MUST support explicit token-retention modes: memory-only (the default), session-scoped, and persistent only by explicit user choice with clear disclosure and a clear-token action. Whether persistent retention of the refresh token remains acceptable is decided by DF-FIDES-2026-0015 (FID-SES-008).

- Priority: P0
- Owner: Fides.Client
- Status: implemented-verified
- Change: revised
- Source: SIG ADM-071, CHX-023; LIMEN-INDEXEDDB-REQUIREMENTS
- Rationale: Revised to link the security review of the modes; behaviour unchanged.
- Depends on: FID-TB-002
- Acceptance: Memory-only writes nothing; session-scoped writes the tab store only; persistent requires consent made from a disclosure.
- Positive test: ClientTests: session-scoped retention keeps the session in this tab only.
- Negative test: ClientTests: memory-only writes the session to no browser store; persistent needs consent and sign-out clears it.
- Evidence: src/Fides.Client/Model.fs; src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-CLI-003** The client MUST expose typed session states (signed out, signing in, signed in, expired, revoked, provider unavailable) and SHOULD signal changes across tabs. Applications MUST still rely on provider checks rather than on the cross-tab signal.

- Priority: P0
- Owner: Fides.Client
- Status: implemented-verified
- Change: retained
- Source: SIG ADM-056, SIG ADM-072
- Rationale: Applications and Arca must branch on states, not exceptions.
- Depends on: FID-TB-005
- Acceptance: Every state is reachable and reported; messages carry no token.
- Positive test: ClientAcceptanceTests session scenarios.
- Negative test: ClientTests: cross-tab messages never carry a token; a message for another application is ignored.
- Evidence: src/Fides.Client/Model.fs; tests/Fides.Tests/ClientTests.fs; tests/Fides.Tests/Acceptance/ClientAcceptanceTests.fs
- Levels achieved: unit, simulated-provider
- Levels required: unit

**FID-CLI-004** The client MUST be published as a pinned, versioned package through the Echelon release contract.

- Priority: P1
- Owner: Fides.Release
- Status: implemented-verified
- Change: retained
- Source: shared foundations (dependency pinning)
- Rationale: Consumers pin exact versions.
- Depends on: FID-CLI-001
- Acceptance: Release 0.2.0 is attested and recorded in echelon-registry (echelon-current pins fides 0.2.0).
- Positive test: ReleaseTests: the release declares every packable project and nothing else.
- Negative test: ReleaseTests: the version is a release version (no pre-release or unpinned version).
- Evidence: .github/workflows/release.yml; release/echelon.release-input.json; tests/Fides.Tests/ReleaseTests.fs
- Levels achieved: unit
- Levels required: unit

## 8. Verification obligations (FID-TEST)

**FID-TEST-001** Executable acceptance tests MUST exist for: invalid state/nonce, replay, wrong callback, revoked identity, expired session, repository access denial and provider outage.

- Priority: P0
- Owner: Fides.Governance
- Status: implemented-verified
- Change: retained
- Source: issue #1
- Rationale: Tests written against the protocol, before the implementation.
- Depends on: FID-EXC-001
- Acceptance: AcceptanceCatalogueTests: every category has scenarios, all pass.
- Positive test: ExchangeAcceptanceTests; ClientAcceptanceTests.
- Negative test: AcceptanceCatalogueTests: every issue #1 category has an acceptance scenario; scenarios refused before the provider expect a refusal on their only step.
- Evidence: tests/Fides.Acceptance/Scenarios.fs; tests/Fides.Tests/Acceptance
- Levels achieved: unit, simulated-provider
- Levels required: simulated-provider

**FID-TEST-002** The hosting adapters MUST pass a shared adapter conformance suite.

- Priority: P1
- Owner: Fides.Hosting
- Status: implemented-verified
- Change: retained
- Source: FID-HOST-004
- Rationale: Adapters must not drift from the core.
- Depends on: FID-HOST-001
- Acceptance: Every scenario and 12 HTTP cases give the core's exact status, body and headers.
- Positive test: ConformanceTests.
- Negative test: ConformanceSuiteTests: an adapter that rewrites a status does not conform even when the scenario would pass.
- Evidence: tests/Fides.Acceptance/Conformance.fs
- Levels achieved: unit, emulated-host
- Levels required: emulated-host

**FID-TEST-003** Every requirement MUST be a record with priority, owner, status, change, source, rationale, dependencies, acceptance criteria, positive test, negative test, evidence and verification levels, and the record set MUST parse without findings.

- Priority: P0
- Owner: Fides.Governance
- Status: implemented-verified
- Change: added
- Source: WI-0022 mission phase 4
- Rationale: A requirement without acceptance criteria and tests cannot be independently verified.
- Depends on: none
- Acceptance: RequirementsCatalogueTests report no structural finding.
- Positive test: RequirementsCatalogueTests: every requirement has every field with an allowed value.
- Negative test: RequirementsCatalogueTests: a record missing a field, with an unknown status or a dangling dependency is reported.
- Evidence: tests/Fides.Tests/RequirementsCatalogue.fs; tests/Fides.Tests/RequirementsCatalogueTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-TEST-004** A requirement's status MUST agree with the repository: `implemented-verified` needs a test that declares it at every required level; a requirement a test claims cannot be `missing`, `blocked` or `deferred`; every requirement that is not verified MUST be named by an open work item; a simulated test MUST NOT satisfy a `live-provider` level.

- Priority: P0
- Owner: Fides.Governance
- Status: implemented-verified
- Change: added
- Source: WI-0022 mission phases 4 and 5
- Rationale: Prevents marking incomplete requirements complete and claiming live properties from simulation.
- Depends on: FID-TEST-003
- Acceptance: RequirementsCatalogueTests report no status finding.
- Positive test: RequirementsCatalogueTests: every requirement's status agrees with the tests and open work.
- Negative test: RequirementsCatalogueTests: a simulated test does not verify a requirement that needs the live provider; claiming verification without a test is reported.
- Evidence: tests/Fides.Tests/RequirementsCatalogueTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-TEST-005** The requirement-to-implementation-to-test matrix MUST be generated from this catalogue, and the build MUST fail when the committed matrix differs.

- Priority: P1
- Owner: Fides.Governance
- Status: implemented-verified
- Change: added
- Source: WI-0022 mission phase 5
- Rationale: A hand-maintained matrix drifts.
- Depends on: FID-TEST-003
- Acceptance: TRACEABILITY-MATRIX.md's generated section equals the rendering.
- Positive test: RequirementsCatalogueTests: the traceability matrix is generated from the catalogue.
- Negative test: RequirementsCatalogueTests: the matrix lists every requirement and counts every status (synthetic).
- Evidence: docs/requirements/TRACEABILITY-MATRIX.md
- Levels achieved: unit
- Levels required: unit

**FID-TEST-006** Cross-tenant tests MUST show that a principal of one tenant cannot read, write, list or learn the existence of another tenant's data through any Fides-provided path, at the real enforcement point (GitHub installation and repository permission, or a future server-side enforcement point), not through mocked UI behaviour.

- Priority: P0
- Owner: Fides.Governance
- Status: blocked
- Change: added
- Source: WI-0022 mission phase 4
- Rationale: Tenant isolation is only real where it is enforced (SEC-THR-108).
- Depends on: FID-ORG-001, FID-DATA-003
- Acceptance: A live-provider test with two installations shows refusal for every cross-tenant attempt.
- Positive test: Same principal reads its own tenant's repository.
- Negative test: Token of tenant A against tenant B's repository, through Arca and directly against the GitHub API: refused.
- Evidence: none
- Levels achieved: none
- Levels required: simulated-provider, live-provider

**FID-TEST-007** Privilege-escalation tests MUST show that no principal can grant itself or another principal a role, capability, organization membership or repository reach it does not already hold, through any Fides or application path.

- Priority: P0
- Owner: Fides.Governance
- Status: blocked
- Change: added
- Source: WI-0022 mission phase 4
- Rationale: SEC-THR-110.
- Depends on: FID-AUZ-006
- Acceptance: Every assignment operation refuses a self-elevation and a cross-tenant assignment.
- Positive test: An authorized administrator assigns a role within their organization.
- Negative test: A member edits the role record in the data repository directly; a member assigns themself administrator; an administrator of org A assigns in org B.
- Evidence: none
- Levels achieved: none
- Levels required: unit, simulated-provider, live-provider

**FID-TEST-008** Malformed-input tests MUST cover the callback query and every exchange endpoint with property-based or fuzzed inputs, and MUST show that no malformed input reaches the provider or yields a token.

- Priority: P1
- Owner: Fides.Governance
- Status: missing
- Change: added
- Source: WI-0022 mission phase 4; SEC-THR-122
- Rationale: Example-based tests cover known shapes only.
- Depends on: FID-EXC-001
- Acceptance: A generator-driven test runs on every build with a fixed seed set.
- Positive test: Generated valid requests succeed against the simulator.
- Negative test: Generated malformed queries and bodies give `state_invalid` or `malformed_request` with no provider call.
- Evidence: none
- Levels achieved: none
- Levels required: unit, simulated-provider

**FID-TEST-009** Unauthorized-data-access tests MUST show that a signed-out, expired, revoked or under-permitted principal gets no data through Arca with a Fides token provider, and that Arca mutates nothing when the provider refuses.

- Priority: P0
- Owner: Fides.Governance
- Status: missing
- Change: added
- Source: WI-0022 mission phase 4; ARCA-AUTH-004
- Rationale: The data path is the asset.
- Depends on: FID-DATA-001
- Acceptance: For each TokenUnavailable case, an Arca read and write through the bridge is refused with no mutation.
- Positive test: A signed-in principal reads through Arca against the simulator.
- Negative test: NoToken, Expired, Revoked and ProviderFailed each refuse the operation.
- Evidence: none
- Levels achieved: none
- Levels required: unit, simulated-provider

**FID-TEST-010** A live-provider contract suite MUST confirm against a real test GitHub App the provider assumptions Fides relies on: token lifetimes, refresh-token rotation, single-use authorization codes, PKCE enforcement, `GET /user` shape and repository-access status codes.

- Priority: P0
- Owner: Fides.Governance
- Status: blocked
- Change: added
- Source: WI-0022 mission phase 4 ("never claim a live property from simulation")
- Rationale: Every provider property above is verified only against Fides's own simulator.
- Depends on: FID-PRV-002
- Acceptance: The suite runs on demand against a test environment App and records each result as live evidence. Blocked: no test GitHub App is registered.
- Positive test: Code exchange, refresh and identity succeed live.
- Negative test: Replayed code, wrong verifier and reused refresh token are refused live.
- Evidence: none
- Levels achieved: none
- Levels required: live-provider

**FID-TEST-011** A live test MUST establish whether a used refresh token is invalidated after rotation, and the result MUST be recorded as evidence for FID-SES-006.

- Priority: P0
- Owner: Fides.Governance
- Status: blocked
- Change: added
- Source: SEC-THR-105
- Rationale: The client's single-flight design assumes single use.
- Depends on: FID-TEST-010
- Acceptance: Live evidence recorded.
- Positive test: Rotation returns a new pair.
- Negative test: The old refresh token is refused after rotation.
- Evidence: none
- Levels achieved: none
- Levels required: live-provider

**FID-TEST-012** A live test MUST establish whether revoking the access token at GitHub invalidates its refresh token, and the result MUST be recorded as evidence for FID-SES-004.

- Priority: P0
- Owner: Fides.Governance
- Status: blocked
- Change: added
- Source: SEC-THR-118
- Rationale: Sign-out may leave a usable refresh grant.
- Depends on: FID-TEST-010
- Acceptance: Live evidence recorded; FID-SES-004 updated.
- Positive test: Revocation answers 204.
- Negative test: Refresh with the paired refresh token after revocation.
- Evidence: none
- Levels achieved: none
- Levels required: live-provider

**FID-TEST-013** A live test MUST confirm that revoking the GitHub App authorization (or uninstalling the App) is reported by Fides as `revoked` on the next refresh or identity call.

- Priority: P1
- Owner: Fides.Governance
- Status: blocked
- Change: added
- Source: SEC-THR-111
- Rationale: External revocation must propagate.
- Depends on: FID-TEST-010
- Acceptance: Live evidence recorded.
- Positive test: Before revocation, refresh succeeds.
- Negative test: After revocation, refresh is `refresh_rejected` and the client state is `revoked`.
- Evidence: none
- Levels achieved: none
- Levels required: live-provider

**FID-TEST-014** Requirement coverage MUST be evaluated by Dokimos in CI: the xUnit traceability gates run inside the Dokimos-evaluated test run, and, once Fides adopts a Dokimos release with requirement verification (0.11 or later) and a Praxis requirement catalogue, UI requirements MUST be verified through `dokimos verification requirements`.

- Priority: P1
- Owner: Fides.Governance
- Status: implemented-unverified
- Change: added
- Source: WI-0022 mission phase 4; Dokimos 0.11.1 docs/requirement-verification.md
- Rationale: Fides pins Dokimos 0.2.0, which gates build and test results only.
- Depends on: FID-TEST-004
- Acceptance: Part 1 holds today (the Dokimos workflow runs `dotnet test`, whose failures it ratchets). Part 2 needs the Dokimos and Praxis upgrades.
- Positive test: A Dokimos run shows the catalogue tests in its TRX evidence.
- Negative test: A catalogue finding fails `tests.failed` and therefore the Dokimos gate.
- Evidence: .github/workflows/dokimos.yml; .dokimos/policy.json; docs/requirements/VERIFICATION-PLAN.md
- Levels achieved: document
- Levels required: integration

## 9. Identity and authentication (FID-IDN)

**FID-IDN-001** A person's stable identity key MUST be the provider plus the provider's immutable numeric account id (`subject`); the GitHub `login` MUST be treated as a changeable display value and never as a key.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-verified
- Change: added
- Source: DF-FIDES-2026-0007; Arca ProviderIdentity (Subject is the stable numeric id)
- Rationale: Logins can be renamed and re-registered by someone else (SEC-THR-119).
- Depends on: FID-PRV-003
- Acceptance: The identity carries `provider` and numeric `subject`; `login` is separate.
- Positive test: ProviderTests: the identity comes from GitHub's user endpoint for the token.
- Negative test: ProviderTests: a user response without id or login is a contract violation.
- Evidence: src/Fides/GitHub.fs; src/Fides/Protocol.fs; tests/Fides.Tests/ProviderTests.fs
- Levels achieved: unit, simulated-provider
- Levels required: unit, simulated-provider

**FID-IDN-002** Sign-in MUST fail closed when the provider's identity cannot be established or is incomplete: no session, and the issued token is revoked.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-verified
- Change: added
- Source: WI-0022 phase 2A
- Rationale: A session without a verified identity is an unauthenticated session.
- Depends on: FID-IDN-001
- Acceptance: A missing id or login is `provider_contract_violation`; an identity refusal is `identity_revoked`; the token is revoked in both cases.
- Positive test: ProviderTests: the identity comes from GitHub's user endpoint.
- Negative test: ProviderTests: a user response without id or login is a contract violation; ExchangeAcceptanceTests `revoked-identity`.
- Evidence: src/Fides/Exchange.fs; src/Fides/GitHub.fs; tests/Fides.Tests/ProviderTests.fs
- Levels achieved: unit, simulated-provider
- Levels required: unit, simulated-provider

**FID-IDN-003** The client MUST expose a provider-qualified principal identifier (`github:<subject>`) as the only key applications use for users, memberships and audit; applications MUST NOT key data by login or display name.

- Priority: P1
- Owner: Fides.Client
- Status: missing
- Change: added
- Source: WI-0022 phase 2A (stable identities, provider identity mapping)
- Rationale: A second provider must not collide with GitHub subjects; applications need one key.
- Depends on: FID-IDN-001
- Acceptance: `Identity` exposes a `Principal` value; the client guide and the UI requirements use it.
- Positive test: Two identities with the same subject from different providers yield different principals.
- Negative test: A login rename keeps the principal; a reused login with a new subject yields a new principal.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-IDN-004** A callback MUST be accepted only in the tab and for the application that started the sign-in; a callback carrying another application's or another tab's `state` MUST be refused without an exchange call.

- Priority: P0
- Owner: Fides.Client
- Status: implemented-unverified
- Change: added
- Source: RFC 9700 section 4.7; SEC-THR-101
- Rationale: Pending sign-ins are keyed by application in tab session storage; no test states the cross-application case.
- Depends on: FID-EXC-001
- Acceptance: A test with two client configurations in one tab shows the refusal.
- Positive test: The initiating application's callback completes.
- Negative test: Application B's callback page receives application A's code and state: `state_invalid`, no exchange call.
- Evidence: src/Fides.Client/FidesClient.fs (pendingKey)
- Levels achieved: none
- Levels required: unit

**FID-IDN-005** After a callback, the client MUST remove `code` and `state` from the address bar with a history replacement before anything else, whatever the outcome.

- Priority: P0
- Owner: Fides.Client
- Status: implemented-verified
- Change: added
- Source: TRUST-BOUNDARIES.md section 4; Limen docs/31-routing.md (OAuth parameters removed with replace)
- Rationale: Codes in history, bookmarks or `Referer` leak (SEC-THR-113).
- Depends on: FID-EXC-001
- Acceptance: The address is replaced with the redirect URI on every outcome.
- Positive test: ClientAcceptanceTests `callback-succeeds`.
- Negative test: ClientTests: a callback removes the code and state from the address bar even when refused.
- Evidence: src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-IDN-006** Fides MUST NOT link identities from different providers to one principal automatically (for example by matching e-mail); linking, when a second provider exists, MUST require a fresh authentication with both providers in one session.

- Priority: P3
- Owner: Fides.Core
- Status: deferred
- Change: added
- Source: WI-0022 phase 2A (provider identity mapping)
- Rationale: E-mail matching enables account takeover through a provider that does not verify e-mail.
- Depends on: FID-IDN-003
- Acceptance: Deferred until a second provider is approved.
- Positive test: Linking after two fresh authentications.
- Negative test: Same e-mail, different providers, no explicit link: two principals.
- Evidence: none
- Levels achieved: none
- Levels required: unit, simulated-provider

**FID-IDN-007** Passkeys and multi-factor authentication MUST be provider capabilities: Fides MUST NOT implement its own factor while GitHub is the only provider, and a provider's declaration MUST state which assurance it proves. A first-party passkey factor (for example through Limen's `limen.credentials` pack) needs an accepted decision first.

- Priority: P3
- Owner: Fides.Core
- Status: deferred
- Change: added
- Source: WI-0022 phase 2A; Limen contract/credentials.contract.json
- Rationale: GitHub already enforces 2FA and passkeys for its accounts (DF-FIDES-2026-0007 item 1); a parallel factor needs a verifier Fides does not have.
- Depends on: FID-PRV-001
- Acceptance: Deferred.
- Positive test: A provider declaring MFA assurance exposes it to applications.
- Negative test: An application requiring MFA assurance refuses a provider that does not declare it.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-IDN-008** Fides MUST document that account recovery is the provider's: recovering a GitHub account restores Fides access, Fides cannot detect a recovered or taken-over account, and an application needing that assurance must require recent authentication.

- Priority: P2
- Owner: Fides.Governance
- Status: missing
- Change: added
- Source: WI-0022 phase 2A (account recovery)
- Rationale: Users and operators need to know where recovery happens.
- Depends on: FID-IDN-001
- Acceptance: The client guide and the error screens state it.
- Positive test: Document review in the UI requirements.
- Negative test: n/a: documentation obligation; the threat (SEC-THR-119) is covered by FID-IDN-001.
- Evidence: none
- Levels achieved: none
- Levels required: document

**FID-IDN-009** A provider outage during sign-in MUST leave any existing session and its state unchanged and MUST let the person retry with a fresh `state` and verifier.

- Priority: P1
- Owner: Fides.Client
- Status: implemented-unverified
- Change: added
- Source: WI-0022 phase 2A (provider unavailability)
- Rationale: The current client sets the state to `provider_unavailable` while keeping the session, which misreports a signed-in person; no test covers a signed-in person signing in again during an outage.
- Depends on: FID-EXC-004
- Acceptance: A signed-in session survives a failed re-sign-in with state `signed_in`.
- Positive test: Retry after the outage completes with a new state.
- Negative test: Outage during re-sign-in of a signed-in person: state and token unchanged.
- Evidence: src/Fides.Client/FidesClient.fs (completeCallback)
- Levels achieved: none
- Levels required: unit, simulated-provider

**FID-IDN-010** The identity handed to applications MUST carry the authentication time, so an application can require recent authentication before a sensitive action.

- Priority: P2
- Owner: Fides.Client
- Status: missing
- Change: added
- Source: WI-0022 phase 2A (MFA and step-up as future capabilities)
- Rationale: Step-up needs a trustworthy time of the last interactive sign-in.
- Depends on: FID-IDN-003
- Acceptance: `authenticatedAt` is set at `/v1/token` and kept unchanged by refresh.
- Positive test: Sign-in records the time; refresh keeps it.
- Negative test: A refreshed session does not reset the authentication time.
- Evidence: none
- Levels achieved: none
- Levels required: unit

## 10. Sessions (FID-SES)

**FID-SES-001** Each application MUST be able to set a maximum session lifetime shorter than the provider's refresh-token lifetime, after which the person must sign in again.

- Priority: P1
- Owner: Fides.Client
- Status: missing
- Change: added
- Source: WI-0022 phase 2A (session expiration)
- Rationale: A six-month refresh grant is longer than most applications should keep a session.
- Depends on: FID-IDN-010
- Acceptance: Past the configured lifetime the token provider answers `expired` and the session is cleared.
- Positive test: Within the lifetime, refresh continues.
- Negative test: Past the lifetime, refresh is not attempted and the state is `expired`.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-SES-002** An application MAY set an idle timeout; when set, a session idle beyond it MUST end as `expired`.

- Priority: P2
- Owner: Fides.Client
- Status: missing
- Change: added
- Source: WI-0022 phase 2A
- Rationale: Shared devices.
- Depends on: FID-SES-001
- Acceptance: Idle time is measured from the last token request.
- Positive test: Activity within the timeout keeps the session.
- Negative test: Idle beyond the timeout ends it.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-SES-003** Refresh MUST be serialized across the application's tabs (for example with the Web Locks API through a Limen port), so two tabs never present the same refresh token.

- Priority: P1
- Owner: Fides.Client
- Status: missing
- Change: added
- Source: WI-0022 phase 2G (concurrent refresh); SEC-THR-105
- Rationale: Single-flight is per tab today; across tabs the client recovers by adopting a sibling's stored session, which works only for stored retention modes.
- Depends on: FID-REL-004
- Acceptance: Two tabs needing a refresh at once make one exchange call.
- Positive test: Two simulated tabs, one refresh.
- Negative test: Memory-only retention in two tabs: neither tab is signed out by the other's rotation.
- Evidence: none
- Levels achieved: none
- Levels required: unit, browser-wasm

**FID-SES-004** Sign-out MUST leave no usable grant: if revoking the access token does not invalidate its refresh token (FID-TEST-012), sign-out MUST also invalidate the refresh grant.

- Priority: P0
- Owner: Fides.Core
- Status: blocked
- Change: added
- Source: WI-0022 phase 2A (logout, revocation); SEC-THR-118
- Rationale: Today sign-out revokes only the access token; the refresh token's fate is unknown.
- Depends on: FID-TEST-012
- Acceptance: After sign-out, a refresh with the old refresh token is refused live.
- Positive test: Sign-out answers `revoked_at_provider`.
- Negative test: Refresh with the pre-sign-out refresh token after sign-out: `refresh_rejected`.
- Evidence: none
- Levels achieved: none
- Levels required: simulated-provider, live-provider

**FID-SES-005** A `401` reported by an application or Arca for the access token MUST trigger one refresh attempt before the session is declared `revoked`; only a refused refresh grant means revoked.

- Priority: P0
- Owner: Fides.Client
- Status: missing
- Change: added
- Source: WI-0022 phase 2G (clock skew, graceful recovery); SEC-THR-112
- Rationale: Violation: `ReportUnauthorized` declares the session revoked on the first 401, so clock skew or an early expiry signs the person out.
- Depends on: FID-REL-006
- Acceptance: A 401 with a valid refresh grant yields a new token; a 401 with a refused grant yields `revoked`.
- Positive test: 401 then successful refresh: state `signed_in`.
- Negative test: 401 then `refresh_rejected`: state `revoked`, stores cleared, other tabs told.
- Evidence: none
- Levels achieved: none
- Levels required: unit, simulated-provider

**FID-SES-006** The client MUST never present a refresh token that has already been rotated, and on a refused refresh MUST first adopt a newer session a sibling tab stored. Whether GitHub refuses a rotated refresh token is FID-TEST-011.

- Priority: P0
- Owner: Fides.Client
- Status: implemented-unverified
- Change: added
- Source: SEC-THR-105; DF-FIDES-2026-0012
- Rationale: Fides's side is unit-tested under FID-REL-004; the provider property it depends on is not verified live.
- Depends on: FID-REL-004, FID-TEST-011
- Acceptance: Unit evidence plus live evidence of single use.
- Positive test: ClientTests: a refused refresh adopts a session another tab already rotated.
- Negative test: Live: the rotated refresh token is refused.
- Evidence: src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs
- Levels achieved: unit
- Levels required: unit, live-provider

**FID-SES-007** Sign-out or revocation in one tab MUST end the session in every tab of the same application, and a message for another application MUST be ignored.

- Priority: P1
- Owner: Fides.Client
- Status: implemented-verified
- Change: added
- Source: SIG ADM-072; WI-0022 phase 2G (browser-tab synchronization)
- Rationale: A session must not live on in a forgotten tab.
- Depends on: FID-CLI-003
- Acceptance: Receiving `signed_out` or `revoked` clears the tab's session.
- Positive test: ClientTests: another tab signing out signs this tab out.
- Negative test: ClientTests: a message for another application is ignored.
- Evidence: src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-SES-008** Persistent retention MUST NOT keep the refresh token in script-readable storage unless the owner accepts DF-FIDES-2026-0015's risk; until decided, the client's persistent mode stays as released (0.2.0) and applications MUST NOT enable it by default.

- Priority: P0
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: DF-FIDES-2026-0015; SEC-THR-106
- Rationale: A six-month refresh token in local storage turns one script injection into six months of repository access.
- Depends on: FID-CLI-002
- Acceptance: DF-FIDES-2026-0015 accepted or rejected, and the client changed accordingly through a versioned release.
- Positive test: Memory-only and session-scoped modes unchanged.
- Negative test: Persistent mode does not expose a refresh token to page script (sealed-cookie option) or is refused without the accepted decision.
- Evidence: none
- Levels achieved: none
- Levels required: unit, browser-wasm

**FID-SES-009** Restoring a session on page load MUST discard a stored session whose refresh token has expired and report `expired`, and MUST NOT trust a stored identity without its grant.

- Priority: P1
- Owner: Fides.Client
- Status: implemented-unverified
- Change: added
- Source: WI-0022 phase 2A (session refresh)
- Rationale: Implemented in `restore`; no test declares it.
- Depends on: FID-CLI-002
- Acceptance: Unit test of restore with an expired stored session.
- Positive test: A valid stored session restores as `signed_in`.
- Negative test: An expired stored session is removed from both stores and reported `expired`; a corrupt stored session is ignored.
- Evidence: src/Fides.Client/FidesClient.fs (restore)
- Levels achieved: none
- Levels required: unit

**FID-SES-010** A "sign out everywhere" action SHOULD revoke the person's whole grant for the GitHub App, with a disclosure that it signs the person out of every application sharing that App.

- Priority: P2
- Owner: Fides.Core
- Status: blocked
- Change: added
- Source: DF-FIDES-2026-0004 consequence; DF-FIDES-2026-0012
- Rationale: The stateless exchange has no central session list; the grant is the only global handle.
- Depends on: FID-SES-004, FID-DATA-006
- Acceptance: Decision accepted; endpoint added in a versioned protocol change.
- Positive test: After sign-out everywhere, every tab's next refresh is refused.
- Negative test: Another person's grant is untouched.
- Evidence: none
- Levels achieved: none
- Levels required: simulated-provider, live-provider

## 11. Non-human identities (FID-SVC)

**FID-SVC-001** Non-human principals (bots, installations, automation) MUST be distinct principal kinds, mirroring Arca's `IdentityKind` (User, Bot, Installation), and MUST NOT use a person's user-to-server token.

- Priority: P1
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2A (human and non-human identities); Arca 0.7.0 src/Arca.Core/Credentials.fs
- Rationale: A person's token acting for automation hides who acted and outlives the person's intent (SEC-THR-120).
- Depends on: FID-IDN-003
- Acceptance: DF-FIDES-2026-0014 accepted; the principal type has a kind.
- Positive test: An installation principal is reported with kind Installation.
- Negative test: Automation presenting a user token is refused by the service contract.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-SVC-002** Services MUST authenticate with GitHub App installation tokens minted in the service's own runtime from the App private key held in that runtime's secret store; the browser-facing exchange MUST NOT mint installation tokens.

- Priority: P1
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: DF-FIDES-2026-0014
- Rationale: The exchange is public; a private key there makes every application a potential installation-token oracle.
- Depends on: FID-SVC-001
- Acceptance: Decision accepted; a separate package or module defines the service contract.
- Positive test: A service mints a token for its installation.
- Negative test: A request to the public exchange for an installation token is `not_found`.
- Evidence: none
- Levels achieved: none
- Levels required: unit, live-provider

**FID-SVC-003** A service's installation token MUST be requested for the narrowest repository set and permissions the operation needs and MUST NOT be cached beyond its expiry.

- Priority: P1
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2C (service identity permissions)
- Rationale: Least privilege for automation.
- Depends on: FID-SVC-002
- Acceptance: Token requests name repositories and permissions.
- Positive test: The requested scope equals the operation's declared need.
- Negative test: A request for broader permissions than declared is refused before calling GitHub.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-SVC-004** Every action a service performs MUST be attributable to the service principal in audit records.

- Priority: P2
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2F (audit)
- Rationale: Incident response.
- Depends on: FID-SVC-001, FID-OPS-007
- Acceptance: Audit lines carry the service principal.
- Positive test: A service action's audit names the service.
- Negative test: n/a: covered by FID-OPS-002 (no token in audit).
- Evidence: none
- Levels achieved: none
- Levels required: unit

## 12. Authorization (FID-AUZ)

**FID-AUZ-001** Authentication and authorization MUST have distinct owners: Fides authenticates; the component holding the enforcement point authorizes (GitHub for repository reach, the application or a server-side enforcement point for application capabilities). Fides MUST NOT provide a centralized authorization service without an accepted decision.

- Priority: P0
- Owner: Fides.Governance
- Status: implemented-unverified
- Change: added
- Source: WI-0022 phase 2B; ARCA-AUTH-005; DF-FIDES-2026-0010 (proposed)
- Rationale: Unowned authorization is unenforced authorization.
- Depends on: FID-TB-004
- Acceptance: AUTHORIZATION-OWNERSHIP.md names an owner for every decision kind; DF-FIDES-2026-0010 accepted.
- Positive test: Document review against the ownership table.
- Negative test: No Fides package exposes an authorization decision API (reference check).
- Evidence: docs/architecture/AUTHORIZATION-OWNERSHIP.md
- Levels achieved: document
- Levels required: document, unit

**FID-AUZ-002** Every protected operation MUST be enforced at a point the requesting user cannot bypass; hidden UI controls, disabled buttons and Limen route guards MUST NOT be the authorization for any operation.

- Priority: P0
- Owner: Application
- Status: missing
- Change: added
- Source: WI-0022 phase 2B; Limen docs/31-routing.md (guards are not an authorization boundary)
- Rationale: The browser is under the user's control (SEC-THR-109).
- Depends on: FID-AUZ-001
- Acceptance: Each consuming application lists its protected operations and their enforcement points; the list is reviewed before production.
- Positive test: A permitted operation succeeds at the enforcement point.
- Negative test: The same operation sent directly (GitHub API with the user token, or the server endpoint) with the UI control hidden is refused.
- Evidence: none
- Levels achieved: none
- Levels required: simulated-provider, live-provider

**FID-AUZ-003** Authorization decisions MUST default to deny: an unknown principal, action, resource, tenant, missing permission data or an unavailable policy input MUST yield deny.

- Priority: P0
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2B (default deny, fail closed)
- Rationale: Fail-open authorization grants access during outages.
- Depends on: FID-AUZ-001
- Acceptance: DF-FIDES-2026-0010 decides who supplies the decision vocabulary; tests show deny for each unknown.
- Positive test: A complete, matching input permits.
- Negative test: Each missing or unknown input denies.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-AUZ-004** An application permission narrower than the user's GitHub repository permission MUST either be enforced by a server-side enforcement point holding its own identity, or be documented to users and operators as advisory. Data needing a narrower boundary MUST live in a separate repository (ARCA-LOC-004).

- Priority: P0
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: DF-FIDES-2026-0013; SEC-THR-109
- Rationale: The user holds the token and can call GitHub directly.
- Depends on: FID-AUZ-001
- Acceptance: DF-FIDES-2026-0013 accepted; each application's permission model classifies every permission as repository-enforced, server-enforced or advisory.
- Positive test: A repository-enforced permission is refused by GitHub for a user without access.
- Negative test: A user with repository write but without the application role edits data directly through the GitHub API; the classification must have predicted it.
- Evidence: none
- Levels achieved: none
- Levels required: live-provider

**FID-AUZ-005** Application authorization decisions MUST be resource-level: they take the principal, the action, the resource and the tenant, not a role alone.

- Priority: P1
- Owner: Application
- Status: blocked
- Change: added
- Source: WI-0022 phase 2B (resource-level decisions)
- Rationale: Roles alone cannot express per-record ownership.
- Depends on: FID-AUZ-003
- Acceptance: The decision vocabulary chosen under DF-FIDES-2026-0010 has all four inputs.
- Positive test: Owner of a record may edit it.
- Negative test: Same role, different record owner: deny.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-AUZ-006** Role and permission assignment, where an application approves it, MUST be stored where only administrators of that tenant can write (a separate repository per ARCA-LOC-004, or a server-side store) and MUST record who assigned what, when.

- Priority: P1
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2B (role and permission assignment)
- Rationale: A role record writable by members is self-assignable.
- Depends on: FID-AUZ-004, FID-ORG-002
- Acceptance: Decision accepted; storage location per application documented.
- Positive test: An administrator's assignment is recorded.
- Negative test: A member's write to the role record is refused by GitHub.
- Evidence: none
- Levels achieved: none
- Levels required: live-provider

**FID-AUZ-007** No principal MAY grant a role, capability or membership it does not hold, or grant within a tenant it does not administer.

- Priority: P0
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2B (privilege escalation prevention)
- Rationale: SEC-THR-110.
- Depends on: FID-AUZ-006
- Acceptance: Covered by FID-TEST-007.
- Positive test: An administrator grants a capability they hold.
- Negative test: Self-elevation and cross-tenant grants are refused.
- Evidence: none
- Levels achieved: none
- Levels required: unit, live-provider

**FID-AUZ-008** An application's required-repository check MUST be repeated on every refresh, and a refresh whose token can no longer read the required repository MUST end the session.

- Priority: P1
- Owner: Fides.Core
- Status: missing
- Change: added
- Source: WI-0022 phase 2B (stale decisions); SEC-THR-111
- Rationale: Access removed at GitHub currently survives until the refresh grant expires.
- Depends on: FID-TB-004
- Acceptance: `/v1/refresh` performs the check; refusal revokes the new token and answers `repository_access_denied`.
- Positive test: Refresh with access keeps the session.
- Negative test: Access removed between sign-in and refresh: `repository_access_denied`, new token revoked.
- Evidence: none
- Levels achieved: none
- Levels required: unit, simulated-provider

**FID-AUZ-009** Refresh and revocation MUST be bound to the application whose sign-in issued the grant: a refresh token issued through application A MUST NOT be refreshable through application B's registration.

- Priority: P0
- Owner: Fides.Core
- Status: missing
- Change: added
- Source: WI-0022 phase 2C (confused deputy); SEC-THR-107
- Rationale: Violation: the exchange accepts any registered application's refresh of any refresh token, because clients are keyed by provider.
- Depends on: FID-DATA-006
- Acceptance: With per-application provider clients the provider refuses cross-application refresh; with a shared App the exchange refuses it from a binding it can verify statelessly.
- Positive test: Application A refreshes its own grant.
- Negative test: Application B's origin and id refresh A's refresh token: refused.
- Evidence: none
- Levels achieved: none
- Levels required: unit, simulated-provider, live-provider

**FID-AUZ-010** The GitHub App MUST request only the permissions Arca needs (repository contents read and write, metadata read) and no organization or account permissions; the hosting guide MUST list them and a test MUST check the guide.

- Priority: P1
- Owner: Operator
- Status: implemented-unverified
- Change: added
- Source: WI-0022 phase 2B (least privilege); DF-FIDES-2026-0003
- Rationale: The App's permissions bound every token.
- Depends on: FID-PRV-002
- Acceptance: The guide lists exactly these permissions; a registered App's manifest matches.
- Positive test: The guide documents the permission set.
- Negative test: A guide listing an extra permission fails the check.
- Evidence: docs/hosting/AWS.md
- Levels achieved: document
- Levels required: unit, live-provider

**FID-AUZ-011** Removing a person's access MUST take effect at each enforcement point within a stated bound: GitHub repository removal at the next GitHub call; application role removal at the next authorization decision; Fides session at the next refresh (FID-AUZ-008).

- Priority: P1
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2B (authorization revocation)
- Rationale: Revocation without a bound is not revocation.
- Depends on: FID-AUZ-008, FID-AUZ-006
- Acceptance: Bounds accepted by the owner and tested.
- Positive test: Before removal, access works.
- Negative test: After removal, access fails within the bound.
- Evidence: none
- Levels achieved: none
- Levels required: live-provider

## 13. Data-layer security (FID-DATA)

**FID-DATA-001** Arca MUST receive credentials only through its token-provider port, as a short-lived access token; the refresh token, client secret and verifier MUST never reach Arca.

- Priority: P0
- Owner: Fides.Arca
- Status: implemented-verified
- Change: added
- Source: ARCA-AUTH-001, ARCA-AUTH-002; DF-FIDES-2026-0008
- Rationale: Arca persists queues and records; it must hold nothing long-lived.
- Depends on: FID-CLI-001
- Acceptance: The bridge passes `AccessToken` only and maps every failure case.
- Positive test: ArcaBridgeTests: Arca sends the Fides token as its bearer credential.
- Negative test: ArcaBridgeTests: every Fides failure is the same Arca failure (no token on failure).
- Evidence: src/Fides.Arca/TokenBridge.fs; tests/Fides.Tests/ArcaBridgeTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-DATA-002** The authoritative data permission check for Arca-stored data MUST be GitHub's repository permission for the user's token; Fides and applications MUST NOT claim a data protection that GitHub does not enforce.

- Priority: P0
- Owner: Arca
- Status: implemented-unverified
- Change: added
- Source: DF-FIDES-2026-0013; ARCA-LOC-004
- Rationale: Clarifies which layer is authoritative.
- Depends on: FID-AUZ-004
- Acceptance: SECURITY-ARCHITECTURE.md's authority table; live test under FID-TEST-006.
- Positive test: A user with read access reads through Arca.
- Negative test: A user without access is refused by GitHub, not by Fides.
- Evidence: docs/architecture/SECURITY-ARCHITECTURE.md
- Levels achieved: document
- Levels required: document, live-provider

**FID-DATA-003** The tenant of any data operation MUST be derived from trusted context (the configured repository location and the installation the token reaches), never from a user-supplied parameter such as a URL segment, query or body field.

- Priority: P0
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2C; SAF-URL-4
- Rationale: A user-chosen tenant parameter is a cross-tenant request waiting to happen (SEC-THR-108).
- Depends on: FID-ORG-001
- Acceptance: DF-FIDES-2026-0011 accepted; the tenant context type cannot be built from request text without validation against memberships.
- Positive test: The tenant comes from configuration and membership.
- Negative test: A URL naming another tenant yields "not permitted", with no request to that tenant's repository.
- Evidence: none
- Levels achieved: none
- Levels required: unit, simulated-provider

**FID-DATA-004** Each tenant's data MUST live in repositories no other tenant's principals can reach (one repository per permission boundary per ARCA-LOC-004 and ARCA-LOC-005); folders inside one repository MUST NOT be used as a tenant boundary.

- Priority: P0
- Owner: Arca
- Status: blocked
- Change: added
- Source: ARCA-LOC-004, ARCA-LOC-005, ARCA-D-007
- Rationale: GitHub enforces permissions per repository, not per folder.
- Depends on: FID-DATA-003
- Acceptance: Organization and tenant document accepted; live cross-tenant test FID-TEST-006 passes.
- Positive test: Tenant A reads its repository.
- Negative test: Tenant A's token reading tenant B's repository is refused live.
- Evidence: none
- Levels achieved: none
- Levels required: live-provider

**FID-DATA-005** If an Echelon application adopts a relational store, application code MUST NOT be granted direct table access; access MUST go through Strata-managed grants and row-level policies whose tenant comes from trusted session context. Strata 0.1.1 is a schema-management CLI with no runtime operation interface, so this requirement has no implementation target today.

- Priority: P2
- Owner: Strata
- Status: deferred
- Change: added
- Source: WI-0022 phase 2C; Strata README (pilot; grants and RLS as schema objects)
- Rationale: The mission asked for "Strata restricted operations"; none exist, and no Fides consumer uses PostgreSQL.
- Depends on: FID-DATA-003
- Acceptance: Revisit when a relational store is approved.
- Positive test: A tenant reads its rows through the policy.
- Negative test: Direct table access by the application role is refused; another tenant's rows are invisible.
- Evidence: none
- Levels achieved: none
- Levels required: integration

**FID-DATA-006** The exchange MUST support a distinct provider client (GitHub App) per application or per declared data-isolation domain, so tokens issued for one domain cannot reach another domain's repositories.

- Priority: P0
- Owner: Fides.Core
- Status: missing
- Change: added
- Source: DF-FIDES-2026-0013; SEC-THR-107; docs/hosting/AWS.md (one App per environment)
- Rationale: Violation: configuration keys clients by provider, so every application in an environment shares one App and one token reach.
- Depends on: FID-PRV-002
- Acceptance: Configuration names the provider client per application; the conformance suite covers two clients.
- Positive test: Application A's sign-in uses A's client id.
- Negative test: Application A's token cannot be refreshed with B's client; a configuration that maps two isolation domains to one client is refused.
- Evidence: none
- Levels achieved: none
- Levels required: unit, simulated-provider

**FID-DATA-007** Arca writes SHOULD carry the acting principal (FID-IDN-003) in commit metadata so sensitive data operations are auditable.

- Priority: P2
- Owner: Arca
- Status: missing
- Change: added
- Source: WI-0022 phase 2C (auditability of sensitive data operations)
- Rationale: GitHub records the token's user; the Fides principal links it to application audit.
- Depends on: FID-IDN-003
- Acceptance: Agreed with Arca's owner; Fides supplies the principal.
- Positive test: A write's commit names the principal.
- Negative test: n/a: auditability, not an access control.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-DATA-008** The exchange MUST perform every repository check with the user's token, never with its client credentials, and MUST NOT read or write application data.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-unverified
- Change: added
- Source: WI-0022 phase 2C (confused deputy)
- Rationale: The exchange holds the client secret; it must not act on data with more authority than the user.
- Depends on: FID-TB-004
- Acceptance: A test asserts the required-repository request carries the user's bearer token and no basic credentials.
- Positive test: Required-repository request uses `Authorization: Bearer <user token>`.
- Negative test: No provider request other than token, refresh and revocation carries the client credentials.
- Evidence: src/Fides/Exchange.fs; src/Fides/GitHub.fs (repositoryAccessRequest)
- Levels achieved: none
- Levels required: unit

## 14. Multiple organizations (FID-ORG)

These requirements are blocked on DF-FIDES-2026-0011. See
[`ORGANIZATION-AND-TENANT-ISOLATION.md`](ORGANIZATION-AND-TENANT-ISOLATION.md).

**FID-ORG-001** An organization MUST have a stable, immutable identifier (the GitHub organization's numeric id, or an Arca organization namespace id per ARCA-LOC-005), never its name.

- Priority: P1
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2D; ARCA-LOC-005
- Rationale: Organization names change and can be re-registered.
- Depends on: FID-IDN-001
- Acceptance: DF-FIDES-2026-0011 accepted.
- Positive test: A renamed organization keeps its id.
- Negative test: A new organization reusing an old name gets a new id.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-ORG-002** A principal MAY belong to several organizations; membership MUST be held by the owner DF-FIDES-2026-0011 names, separately from the identity-provider account.

- Priority: P1
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2D (memberships, separation of accounts and membership)
- Rationale: Identity is not membership.
- Depends on: FID-ORG-001
- Acceptance: Decision accepted; membership type defined.
- Positive test: A principal lists two memberships.
- Negative test: A provider account without membership has no organization access.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-ORG-003** Switching the active organization MUST re-derive the tenant context from memberships, clear organization-scoped cached data and views, and MUST NOT carry a decision from the previous organization.

- Priority: P1
- Owner: Application
- Status: blocked
- Change: added
- Source: WI-0022 phase 2D (organization switching)
- Rationale: Stale views after switching leak data across tenants.
- Depends on: FID-ORG-002, FID-DATA-003
- Acceptance: Switch tested in the UI and in the tenant context type.
- Positive test: Switch to a member organization shows its data only.
- Negative test: Switch to a non-member organization is refused; previous organization's data is gone from the view and cache.
- Evidence: none
- Levels achieved: none
- Levels required: unit, browser-wasm

**FID-ORG-004** Organization administration (members, roles, settings) MUST be scoped to one organization; an administrator of one organization MUST have no administrative effect on another.

- Priority: P2
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2D
- Rationale: Organization-scoped administration.
- Depends on: FID-ORG-002, FID-AUZ-007
- Acceptance: Covered by FID-TEST-007.
- Positive test: An administrator manages their organization.
- Negative test: The same administrator acting on another organization is refused.
- Evidence: none
- Levels achieved: none
- Levels required: unit, live-provider

**FID-ORG-005** Delegated access, if approved, MUST be explicit, time-bound, scoped, revocable and limited to capabilities the delegator holds.

- Priority: P3
- Owner: Owner decision
- Status: deferred
- Change: added
- Source: WI-0022 phase 2D (delegated access)
- Rationale: Delegation is a privilege-escalation path.
- Depends on: FID-AUZ-007
- Acceptance: Deferred until a consumer needs delegation.
- Positive test: A delegate acts within scope until expiry.
- Negative test: Past expiry, or beyond the delegator's capabilities: refused.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-ORG-006** Invitations MUST be single use, expire, and bind to the accepting principal's provider subject; removing a member MUST remove their repository access and application roles in that organization.

- Priority: P2
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2D (invitation and removal)
- Rationale: Reusable invitations are open doors.
- Depends on: FID-ORG-002
- Acceptance: Decision accepted; workflow specified.
- Positive test: An invitation accepted once grants membership.
- Negative test: A second acceptance, an expired invitation, or acceptance by another principal: refused.
- Evidence: none
- Levels achieved: none
- Levels required: unit, live-provider

**FID-ORG-007** The organization context of a request MUST NOT be taken from a user-supplied parameter without validating it against the principal's memberships, and validation failure MUST deny.

- Priority: P0
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2C and 2D
- Rationale: SEC-THR-108.
- Depends on: FID-ORG-002, FID-DATA-003
- Acceptance: Covered by FID-TEST-006.
- Positive test: A member's organization parameter is accepted.
- Negative test: A non-member's organization parameter is denied without contacting that organization's repositories.
- Evidence: none
- Levels achieved: none
- Levels required: unit, simulated-provider

## 15. Application integration and user experience (FID-UX)

These requirements are detailed, with the screen inventory, in
[`UI-REQUIREMENTS-AND-SCREENS.md`](UI-REQUIREMENTS-AND-SCREENS.md).

**FID-UX-001** Fides's client contracts MUST be F# types and functions usable from WebAssembly; browser APIs MUST be reached only through host ports, never from the client library.

- Priority: P1
- Owner: Fides.Client
- Status: implemented-unverified
- Change: added
- Source: WI-0022 phase 2E (F#-first, WASM-compatible contracts)
- Rationale: The client already takes `ClientPorts`; no test forbids a browser API reference.
- Depends on: FID-CLI-001
- Acceptance: A reference test like CoreBoundaryTests for Fides.Client.
- Positive test: The client builds and runs in trimmed browser-wasm.
- Negative test: A JS interop reference in Fides.Client fails the test.
- Evidence: src/Fides.Client/FidesClient.fs (ClientPorts); scripts/verify-wasm.sh
- Levels achieved: browser-wasm
- Levels required: unit, browser-wasm

**FID-UX-002** Fides MUST provide a Limen integration: client ports implemented through Limen packs, the callback route declared in the application's route inventory (`.echelon/routes.json`), deep links resumed through Limen's `captureReturnTo`/`resumeReturnTo` without open redirect, and no credential-like route parameter.

- Priority: P1
- Owner: Fides.Client
- Status: missing
- Change: added
- Source: WI-0022 phase 2E; Limen docs/31-routing.md
- Rationale: Every application otherwise rewrites the same glue.
- Depends on: FID-UX-001
- Acceptance: A Limen engine package passes the client acceptance scenarios in browser-wasm.
- Positive test: Sign-in from a deep link returns to it.
- Negative test: A `returnTo` naming another origin is refused; a route with a `code` parameter is rejected by the route table.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm

**FID-UX-003** Fides screens MUST be built from Forma components and tokens; Fides's own brand MUST be a Forma brand manifest supplied by Fides or the application, and Fides MUST NOT add Echelon branding to Forma or depend on Forma's Echelon default palette.

- Priority: P1
- Owner: Fides.UI
- Status: missing
- Change: added
- Source: WI-0022 phase 2E; Forma requirements/WHITE-LABEL-AND-SKINNING.md (Forma 0.14.0 default palette is still Echelon's)
- Rationale: Applications carry their own brand; Forma must stay brand-neutral.
- Depends on: FID-UX-001
- Acceptance: Screens render with a non-Echelon brand manifest with no Echelon colour.
- Positive test: Rendered sign-in screen uses the supplied manifest's tokens.
- Negative test: Rendering with the example non-Echelon brand shows no Echelon palette value.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm

**FID-UX-004** Fides MUST have its own application logo and a landing page that states what Fides is, what signing in shares with the application, and links to privacy and support information.

- Priority: P2
- Owner: Fides.UI
- Status: missing
- Change: added
- Source: WI-0022 phase 2E
- Rationale: People must recognise the sign-in boundary (SEC-THR-121).
- Depends on: FID-UX-003
- Acceptance: Logo asset and landing screen exist and pass accessibility checks.
- Positive test: Landing page renders with the logo and its text alternative.
- Negative test: Missing logo asset shows the text name, never a broken image.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm, manual-accessibility

**FID-UX-005** Fides MUST provide a dedicated sign-in screen and a dedicated callback screen; the callback screen MUST complete or refuse the callback before rendering any application content.

- Priority: P1
- Owner: Fides.UI
- Status: missing
- Change: added
- Source: WI-0022 phase 2E
- Rationale: The callback page handles codes; it must do nothing else.
- Depends on: FID-UX-003, FID-IDN-005
- Acceptance: Screens exist per the screen inventory.
- Positive test: Callback with a valid state shows progress and then the application.
- Negative test: Callback with an invalid state shows the "sign-in could not be completed" screen and no application content.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm

**FID-UX-006** Every asynchronous wait in a Fides flow (redirect to the provider, callback exchange, session restore, refresh that blocks the UI, sign-out) MUST show a visible, branded Forma loading indicator with an accessible name; a blank screen or a bare text-only "Loading" MUST NOT appear.

- Priority: P1
- Owner: Fides.UI
- Status: missing
- Change: added
- Source: WI-0022 phase 2E; Forma 0.14.0 `.ef-startup`, `spinner`, `aria-busy`
- Rationale: A blank wait during sign-in looks like a failure or a phishing page.
- Depends on: FID-UX-003
- Acceptance: Rendered verification of each wait state; slow and timed-out variants shown.
- Positive test: Each wait state shows the indicator within 100 ms.
- Negative test: With the exchange delayed, no frame is blank and no bare "Loading" text is shown; a wait beyond the timeout shows the outage screen.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm

**FID-UX-007** Error, recovery and session-expiration screens MUST be accessible, state what happened in plain language, offer the recovery action, and show an Aegis-style reference without any secret.

- Priority: P1
- Owner: Fides.UI
- Status: missing
- Change: added
- Source: WI-0022 phase 2E; Forma requirements/AEGIS-FAULT-PRESENTATION.md
- Rationale: People recover from errors they understand.
- Depends on: FID-UX-003
- Acceptance: One screen per refusal family in the inventory.
- Positive test: Each session state and callback outcome maps to a screen.
- Negative test: No screen shows a code, token or provider text; an unknown refusal shows the generic outage screen.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm

**FID-UX-008** Fides screens MUST meet WCAG 2.2 level AA.

- Priority: P1
- Owner: Fides.UI
- Status: missing
- Change: added
- Source: WI-0022 phase 2E; Forma A11Y-001
- Rationale: Sign-in must be usable by everyone.
- Depends on: FID-UX-005
- Acceptance: Automated checks pass and a manual audit is recorded per release.
- Positive test: Automated accessibility scan of each screen.
- Negative test: Contrast, focus-not-obscured and target-size failures are detected by the scan in a seeded defective screen.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm, manual-accessibility

**FID-UX-009** Every Fides flow MUST be operable by keyboard alone and by screen reader: focus moves to the new screen's heading on each state change, and state changes are announced through a live region.

- Priority: P1
- Owner: Fides.UI
- Status: missing
- Change: added
- Source: WI-0022 phase 2E
- Rationale: State changes during sign-in are otherwise silent for screen-reader users.
- Depends on: FID-UX-008
- Acceptance: Keyboard-only and screen-reader walkthroughs recorded.
- Positive test: Keyboard-only sign-in completes.
- Negative test: No keyboard trap; focus never lands on a hidden element.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm, manual-accessibility

**FID-UX-010** The UI MUST distinguish authentication failure (who you are could not be established), authorization denial (you are signed in but not permitted) and provider outage (try again later), each with its own wording and recovery action, derived from typed states and refusals.

- Priority: P1
- Owner: Fides.UI
- Status: missing
- Change: added
- Source: WI-0022 phase 2E; Limen NotPermitted vs NotFound
- Rationale: Telling a denied user to "sign in again", or an outage victim that they are denied, misleads.
- Depends on: FID-UX-007
- Acceptance: A pure mapping from every state and refusal to one of the three classes, tested exhaustively.
- Positive test: Each refusal maps to its class.
- Negative test: No refusal maps to more than one class; `repository_access_denied` is authorization, not authentication; `provider_unavailable` is never shown as denial.
- Evidence: none
- Levels achieved: none
- Levels required: unit, browser-wasm

**FID-UX-011** Folio MUST be used only where a printable security or administration document is actually required; none is required today.

- Priority: P3
- Owner: Owner decision
- Status: deferred
- Change: added
- Source: WI-0022 phase 2E; Folio 0.4.1 (print component system)
- Rationale: No printable document has been identified.
- Depends on: none
- Acceptance: Revisit when an audit export or recovery sheet is requested.
- Positive test: n/a until a document is required.
- Negative test: n/a: no document exists.
- Evidence: none
- Levels achieved: none
- Levels required: document

**FID-UX-012** Fides screens MUST present Aegis faults through Forma's fault components (`fault-blocking`, `fault-inline`, `fault-reference`, `recovery-actions`) once a browser Aegis sink exists; until then they present the client's typed states only.

- Priority: P2
- Owner: Fides.UI
- Status: blocked
- Change: added
- Source: DF-FIDES-2026-0009 item 5; Aegis has no browser sink
- Rationale: Consistent fault presentation across Echelon applications.
- Depends on: FID-UX-007
- Acceptance: Aegis browser sink released.
- Positive test: A classified client fault renders with its reference.
- Negative test: Fault details with secret context render redacted.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm

**FID-UX-013** Sign-in and callback screens MUST NOT be framable (`frame-ancestors 'none'`), MUST reach the provider by top-level navigation (no embedded web view or iframe), and MUST show the provider's real address.

- Priority: P1
- Owner: Application
- Status: missing
- Change: added
- Source: RFC 9700 section 4.15 (clickjacking); SEC-THR-121
- Rationale: Framed sign-in is a clickjacking and phishing vector.
- Depends on: FID-UX-005
- Acceptance: Hosting guidance and a header check for each consuming application.
- Positive test: The callback page response carries `frame-ancestors 'none'`.
- Negative test: Embedding the callback page in a frame is blocked by the browser.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm

**FID-UX-014** An application using Fides MUST ship a Content Security Policy that forbids inline and third-party script (`script-src 'self'` with hashes or nonces only, `object-src 'none'`, `base-uri 'none'`) and SHOULD enable Trusted Types.

- Priority: P0
- Owner: Application
- Status: missing
- Change: added
- Source: DF-FIDES-2026-0015; SEC-THR-106
- Rationale: Every retention mode is readable by script in the origin; CSP is the main control.
- Depends on: FID-CLI-002
- Acceptance: The client guide states the policy; each consumer's deployment is checked before production.
- Positive test: A Fides-using page works under the policy (WASM needs `'wasm-unsafe-eval'` only).
- Negative test: An injected inline script is blocked by the policy.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm

**FID-UX-015** The UI SHOULD warn before a session reaches its maximum lifetime and MUST let the person re-authenticate without losing the current route.

- Priority: P2
- Owner: Fides.UI
- Status: missing
- Change: added
- Source: WI-0022 phase 2E (session-expiration screens)
- Rationale: Unexpected sign-outs lose work.
- Depends on: FID-SES-001, FID-UX-002
- Acceptance: Warning shown at a configurable lead time.
- Positive test: Warning appears and re-authentication resumes the route.
- Negative test: Re-authentication as a different principal does not resume the previous principal's unsaved data.
- Evidence: none
- Levels achieved: none
- Levels required: browser-wasm

## 16. Operational security (FID-OPS)

**FID-OPS-001** The client secret MUST be held only by reference in configuration and read from the secret store at runtime; a cached secret MUST expire within five minutes so rotation takes effect.

- Priority: P0
- Owner: Fides.Hosting
- Status: implemented-verified
- Change: added
- Source: DF-FIDES-2026-0005; FID-HOST-002
- Rationale: Secrets in configuration leak through deployment tooling.
- Depends on: FID-HOST-002
- Acceptance: Configuration rejects anything but a reference; the cache expires.
- Positive test: SecretsTests: the secret cache serves a warm secret until it expires.
- Negative test: ConfigurationTests: the configuration holds a secret reference, never a secret; SecretsTests: the secret cache does not keep a failure.
- Evidence: src/Fides.Hosting/SecretCache.fs; src/Fides/Configuration.fs; tests/Fides.Tests/ConfigurationTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-OPS-002** Platform logs outside Fides's code (API Gateway access logs, Lambda platform lines) MUST NOT record headers, bodies or query strings.

- Priority: P0
- Owner: Fides.Hosting.Aws
- Status: implemented-unverified
- Change: added
- Source: SEC-THR-113
- Rationale: The template's access log format holds only request id, route, status, latency and integration status, but no test pins that.
- Depends on: FID-EXC-005
- Acceptance: A template test asserts the access log format fields are exactly the allowed set.
- Positive test: The format holds the allowed fields.
- Negative test: Adding `$context.authorizer`, a header or a query field fails the test.
- Evidence: infrastructure/aws/fides-exchange.template.json (AccessLogSettings)
- Levels achieved: document
- Levels required: unit

**FID-OPS-003** Configuration faults and provider contract violations MUST raise an operator alarm, not only a log line.

- Priority: P1
- Owner: Fides.Hosting.Aws
- Status: missing
- Change: added
- Source: DF-FIDES-2026-0009; SEC-THR-115
- Rationale: `configuration_unavailable` means sign-in is down for everyone.
- Depends on: FID-EXC-005
- Acceptance: The template declares a metric filter and alarm on `FIDES.CONFIGURATION.UNAVAILABLE` and `FIDES.PROVIDER.CONTRACT_VIOLATION`.
- Positive test: TemplateTests find the alarms.
- Negative test: A template without them fails.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-OPS-004** Every audit line and Aegis fault MUST carry a correlation identifier (the platform request id), and every exchange response MUST return it in a header so a person's support reference can be traced.

- Priority: P1
- Owner: Fides.Hosting
- Status: missing
- Change: added
- Source: WI-0022 phase 2F (audit events with correlation identifiers)
- Rationale: Violation: audit lines carry operation, application, outcome and duration only; the API Gateway request id is not propagated.
- Depends on: FID-EXC-005
- Acceptance: Audit line and fault scope hold the id; response header `X-Fides-Request-Id`.
- Positive test: The id in the response equals the id in the audit line.
- Negative test: A caller-supplied correlation header is not trusted as the id (no log injection).
- Evidence: none
- Levels achieved: none
- Levels required: unit, emulated-host

**FID-OPS-005** The exchange's API MUST enforce stage-level request throttling with operator-set rate and burst, and bounded log retention.

- Priority: P1
- Owner: Fides.Hosting.Aws
- Status: implemented-verified
- Change: added
- Source: WI-0022 phase 2F (rate limiting)
- Rationale: Bounds cost and pressure on GitHub.
- Depends on: FID-HOST-002
- Acceptance: Template parameters drive API Gateway throttling.
- Positive test: TemplateTests: the API uses payload format 2.0, TLS 1.2, throttling and bounded log retention.
- Negative test: The same test fails a template without throttling.
- Evidence: infrastructure/aws/fides-exchange.template.json; tests/Fides.Hosting.Aws.Tests/TemplateTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-OPS-006** The exchange SHOULD limit requests per source and per application, and MUST NOT treat the `Origin` header as authentication of a caller.

- Priority: P2
- Owner: Fides.Hosting.Aws
- Status: missing
- Change: added
- Source: SEC-THR-114
- Rationale: Non-browser callers can forge `Origin`; stage throttling is global.
- Depends on: FID-OPS-005
- Acceptance: An optional WAF rate rule or equivalent documented and templated; threat model states the Origin limit.
- Positive test: Normal traffic passes.
- Negative test: A burst from one source is limited while another source is served.
- Evidence: none
- Levels achieved: none
- Levels required: unit

**FID-OPS-007** Security audit events (sign-in success and refusal, refresh, revocation, repository-access denial) MUST record the principal (FID-IDN-003) when known, the application, the outcome and the correlation id, and nothing secret.

- Priority: P1
- Owner: Fides.Hosting
- Status: missing
- Change: added
- Source: WI-0022 phase 2F (audit events)
- Rationale: Incident response needs who, not only what.
- Depends on: FID-OPS-004, FID-IDN-003
- Acceptance: Audit record type extended; leak scans still pass.
- Positive test: A sign-in audit names the principal.
- Negative test: A refusal before identity is established names no principal; no token appears.
- Evidence: none
- Levels achieved: none
- Levels required: unit, emulated-host

**FID-OPS-008** Configuration MUST be validated at start-up and a deployment MUST fail closed on any problem; loopback (`http://localhost`) redirects and origins MUST be refused unless the environment explicitly opts in.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-verified
- Change: added
- Source: WI-0022 phase 2F (secure configuration, environment separation)
- Rationale: A permissive production configuration is a silent vulnerability.
- Depends on: FID-TB-003
- Acceptance: Parsing reports every problem; loopback needs `allowLoopback`.
- Positive test: ConfigurationTests: the acceptance fixture's configuration parses.
- Negative test: ConfigurationTests: loopback redirects and origins are allowed only when the configuration opts in.
- Evidence: src/Fides/Configuration.fs; src/Fides.Hosting.Aws/Program.fs; tests/Fides.Tests/ConfigurationTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-OPS-009** Builds MUST use pinned dependencies, third-party actions pinned by commit SHA, and a dependency vulnerability check whose failure blocks release; each release MUST include a software bill of materials.

- Priority: P1
- Owner: Fides.Release
- Status: missing
- Change: added
- Source: WI-0022 phase 2F (supply chain); SEC-THR-116
- Rationale: Partial today: NuGet versions are pinned and the Dokimos action is SHA-pinned; other actions are tag-pinned; no vulnerability gate or SBOM.
- Depends on: FID-CLI-004
- Acceptance: Workflows pinned by SHA; `dotnet list package --vulnerable` gate; SBOM asset in the release.
- Positive test: Release workflow produces the SBOM.
- Negative test: A known-vulnerable package fails the gate.
- Evidence: none
- Levels achieved: none
- Levels required: integration

**FID-OPS-010** Consumers MUST verify the Sigstore attestation and checksum of every Fides release asset before use; the install path (Conditor) MUST refuse an unverified asset.

- Priority: P1
- Owner: Fides.Release
- Status: implemented-unverified
- Change: added
- Source: DF-FIDES-2026-0006
- Rationale: Conditor verifies installed feeds; Fides has no test showing a tampered asset is refused.
- Depends on: FID-CLI-004
- Acceptance: Evidence from Conditor's refusal of a tampered Fides asset.
- Positive test: A genuine asset installs.
- Negative test: A modified asset is refused.
- Evidence: .conditor/lock.json; docs/client/README.md
- Levels achieved: document
- Levels required: integration

**FID-OPS-011** The threat model MUST be maintained under Tutela's model and re-reviewed when a decision in it is accepted, when a provider or server-side component is added, and before the first production deployment.

- Priority: P1
- Owner: Fides.Governance
- Status: implemented-unverified
- Change: added
- Source: WI-0022 phase 2F (threat modeling)
- Rationale: A threat model is a living record.
- Depends on: FID-TB-001
- Acceptance: THREAT-MODEL.md reviewed by the owner.
- Positive test: Every SEC-THR names requirements that exist.
- Negative test: A threat naming an undeclared requirement fails review.
- Evidence: docs/security/THREAT-MODEL.md
- Levels achieved: document
- Levels required: document, unit

**FID-OPS-012** Incident-response runbooks MUST exist for client-secret compromise, a leaked token class, a compromised release and a provider-side incident, each with detection signal, containment steps and the requirement it relies on.

- Priority: P1
- Owner: Operator
- Status: missing
- Change: added
- Source: WI-0022 phase 2F (incident response)
- Rationale: Rotation under pressure fails without a rehearsed procedure.
- Depends on: FID-OPS-011
- Acceptance: Runbooks committed and rehearsed once in the test environment.
- Positive test: Rehearsal record.
- Negative test: n/a: procedure; rehearsal evidence stands in.
- Evidence: none
- Levels achieved: none
- Levels required: document, live-provider

**FID-OPS-013** A deployed test environment MUST pass a smoke test (preflight, a refused request, and one live sign-in with the test App) before any production deployment.

- Priority: P1
- Owner: Operator
- Status: blocked
- Change: added
- Source: DF-FIDES-2026-0005; FID-HOST-003 revision
- Rationale: The template has never been deployed.
- Depends on: FID-HOST-003, FID-TEST-010
- Acceptance: Blocked on the operator's AWS account, domain and test GitHub App.
- Positive test: Live sign-in succeeds.
- Negative test: Unregistered origin is refused live.
- Evidence: none
- Levels achieved: none
- Levels required: live-provider

**FID-OPS-014** Client-secret rotation MUST be possible without downtime (GitHub Apps allow a second client secret) and MUST be rehearsed in the test environment.

- Priority: P1
- Owner: Operator
- Status: blocked
- Change: added
- Source: WI-0022 phase 2F (secret rotation)
- Rationale: The documented procedure has never been run.
- Depends on: FID-OPS-001, FID-OPS-013
- Acceptance: Rehearsal record with no failed sign-in during the change.
- Positive test: Sign-in works throughout rotation.
- Negative test: After the old secret is deleted, the exchange stops using it within the cache lifetime.
- Evidence: docs/hosting/AWS.md
- Levels achieved: none
- Levels required: live-provider

## 17. Reliability and performance (FID-REL)

**FID-REL-001** The exchange SHOULD answer within these proposed targets, excluding GitHub's own time: p95 under 300 ms warm, p99 under 2 s including cold start; end-to-end sign-in p95 under 4 s. The targets need owner approval.

- Priority: P2
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2G (explicit latency targets, proposed)
- Rationale: Without targets, regressions are invisible.
- Depends on: FID-OPS-013
- Acceptance: Owner approves the numbers; measured in the test environment.
- Positive test: Load test within targets.
- Negative test: n/a: a performance target, not a control.
- Evidence: none
- Levels achieved: none
- Levels required: live-provider

**FID-REL-002** The exchange SHOULD target 99.9% monthly availability (proposed for owner approval), and a signed-in person MUST keep working while the exchange is down until the access token expires.

- Priority: P2
- Owner: Owner decision
- Status: blocked
- Change: added
- Source: WI-0022 phase 2G (availability targets, proposed)
- Rationale: The stateless design keeps sessions alive through short outages.
- Depends on: FID-REL-001
- Acceptance: Owner approves; the second clause tested in the client.
- Positive test: Exchange down, valid access token: data operations continue.
- Negative test: Exchange down, token needing refresh: `provider_unavailable`, session kept, no grant.
- Evidence: none
- Levels achieved: none
- Levels required: unit, live-provider

**FID-REL-003** Provider failures MUST be classified deterministically: 5xx, 429, a 403 on the identity call, a timeout or a transport failure are `provider_unavailable`; unexpected shapes are `provider_contract_violation`.

- Priority: P0
- Owner: Fides.Core
- Status: implemented-verified
- Change: added
- Source: WI-0022 phase 2G (deterministic behaviour under provider failures)
- Rationale: Non-deterministic classification produces wrong UI and wrong retries.
- Depends on: FID-EXC-004
- Acceptance: Pure classification functions over HTTP outcomes.
- Positive test: ProviderTests: provider failures are outages.
- Negative test: ProviderTests: transport failures are outages.
- Evidence: src/Fides/GitHub.fs; tests/Fides.Tests/ProviderTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-REL-004** Concurrent token requests in one client MUST share one refresh, across threads, and a refused refresh MUST adopt a newer sibling session before reporting `revoked`.

- Priority: P0
- Owner: Fides.Client
- Status: implemented-verified
- Change: added
- Source: WI-0017; WI-0022 phase 2G (concurrent refresh)
- Rationale: Refresh tokens are single use.
- Depends on: FID-CLI-001
- Acceptance: Exactly one refresh call for many concurrent callers.
- Positive test: ClientTests: concurrent token requests share one refresh.
- Negative test: ClientTests: a refused refresh adopts a session another tab already rotated instead of revoking.
- Evidence: src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-REL-005** Cross-tab messages MUST name only what happened and MUST NOT carry a token; a `signed_in` message MUST only prompt a store read.

- Priority: P1
- Owner: Fides.Client
- Status: implemented-verified
- Change: added
- Source: WI-0022 phase 2G (browser-tab synchronization); SEC-THR-123
- Rationale: Messages are readable by every same-origin script.
- Depends on: FID-CLI-003
- Acceptance: Messages are `fides:<application>:<event>`.
- Positive test: ClientTests: cross-tab messages name what happened and never carry a token.
- Negative test: ClientTests: a message for another application is ignored.
- Evidence: src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-REL-006** Token expiry MUST be evaluated on one clock: the client MUST derive the access-token deadline from the lifetime (`expires_in`) measured on its own clock at receipt, or correct for the exchange's clock, and MUST tolerate at least five minutes of browser clock skew.

- Priority: P0
- Owner: Fides.Client
- Status: missing
- Change: added
- Source: WI-0022 phase 2G (clock skew); SEC-THR-112
- Rationale: Violation: the exchange computes absolute expiries on its clock; the client compares them with the browser clock.
- Depends on: FID-CLI-001
- Acceptance: Protocol carries a lifetime or server time; client tests with skewed clocks.
- Positive test: Browser 10 minutes fast: refresh happens at the right lifetime.
- Negative test: Browser 10 minutes slow: an expired token is never handed to Arca.
- Evidence: none
- Levels achieved: none
- Levels required: unit, simulated-provider

**FID-REL-007** Expiry boundaries MUST be exact and fail closed: a token at its expiry instant is expired; the refresh margin applies before expiry; a refresh token at its expiry instant cannot be used.

- Priority: P1
- Owner: Fides.Client
- Status: implemented-unverified
- Change: added
- Source: WI-0022 phase 2G (expiration boundaries)
- Rationale: `Tokens.decide` implements these comparisons; no test pins the boundary instants.
- Depends on: FID-REL-006
- Acceptance: Boundary tests at expiry minus margin, expiry, and refresh expiry.
- Positive test: One tick before the margin: use the token.
- Negative test: At the refresh-token expiry instant: `expired`, no refresh call.
- Evidence: src/Fides.Client/Tokens.fs
- Levels achieved: none
- Levels required: unit

**FID-REL-008** A pending sign-in's lifetime MUST be evaluated on the browser's clock only, and a pending sign-in dated in the future MUST be refused as expired.

- Priority: P1
- Owner: Fides.Client
- Status: implemented-verified
- Change: added
- Source: WI-0022 phase 2G (clock boundaries)
- Rationale: A future-dated pending record would extend its life.
- Depends on: FID-EXC-001
- Acceptance: Both pending and callback times come from the client clock port.
- Positive test: ClientAcceptanceTests `callback-succeeds`.
- Negative test: ClientTests: a pending sign-in from the future is refused as expired.
- Evidence: src/Fides.Client/SignIn.fs; tests/Fides.Tests/ClientTests.fs
- Levels achieved: unit
- Levels required: unit

**FID-REL-009** Timeouts and retries MUST be contractual and documented: each provider call times out at 10 s, the function at 15 s, the API integration at 20 s; the exchange MUST NOT retry a code exchange; refresh MAY be retried by the client only on a later token request.

- Priority: P1
- Owner: Fides.Hosting
- Status: implemented-unverified
- Change: added
- Source: WI-0022 phase 2G (timeout and retry contracts)
- Rationale: Values exist in code and template but are not stated in the protocol or tested together.
- Depends on: FID-REL-003
- Acceptance: EXCHANGE-PROTOCOL.md states them; a test asserts the ordering provider < function < integration.
- Positive test: A slow provider yields `provider_unavailable` within the function timeout.
- Negative test: A code exchange is attempted exactly once even when the provider times out.
- Evidence: src/Fides.Hosting.Aws/Function.fs; infrastructure/aws/fides-exchange.template.json
- Levels achieved: none
- Levels required: unit, emulated-host

**FID-REL-010** Recovery after any failure MUST NOT grant access without a valid token: during and after an outage the token provider MUST answer `provider_unavailable` until a refresh succeeds.

- Priority: P0
- Owner: Fides.Client
- Status: implemented-unverified
- Change: added
- Source: WI-0022 phase 2G (graceful recovery without unauthorized access)
- Rationale: Implemented; the acceptance scenario `expired-session-outage` covers it under FID-CLI-003 but does not declare this requirement.
- Depends on: FID-EXC-004
- Acceptance: A scenario declares FID-REL-010.
- Positive test: Recovery: the next token request after the outage refreshes and succeeds.
- Negative test: During the outage, no token is returned for an expired access token.
- Evidence: src/Fides.Client/FidesClient.fs; tests/Fides.Acceptance/Scenarios.fs (`expired-session-outage`)
- Levels achieved: unit, simulated-provider
- Levels required: unit, simulated-provider
