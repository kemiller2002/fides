---
id: FIDES-THREAT-MODEL
title: Fides threat model
status: draft
version: 0.1.0
created: 2026-11-09
updated: 2026-11-09
owners:
  - fides
related_documents:
  - docs/architecture/SECURITY-ARCHITECTURE.md
  - docs/architecture/TRUST-BOUNDARIES.md
  - docs/requirements/FIDES-REQUIREMENTS.md
  - .tutela/schemas/threat.schema.json
tags: [security, threat-model, tutela, oauth]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:49.505Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
---

# Fides threat model

Status: **draft for owner review** (WI-0022). Tutela's model applies:
asset, trust boundary, threat, invariant, control, evidence, unknown.
Tutela never asserts that a system is secure; this model records which
threats current evidence answers, which it does not, and which are unknown.
Threat IDs follow `.tutela/schemas/threat.schema.json` and start at
`SEC-THR-101` so they never collide with Tutela's own installed profile
(`SEC-THR-001..005`).

**Evidence scope.** Every "Mitigated" below is mitigated *in Fides's code, as
proven by unit, simulated-provider, emulated-host or browser-wasm tests*.
Nothing has been run against live GitHub or a deployed exchange. A
mitigation that depends on GitHub's real behaviour is marked with its
residual unknown and a live test obligation (FID-TEST-010..013).

## 1. Assets

| Asset | Where it lives | Why it matters |
|---|---|---|
| A1 GitHub App client secret | Operator's secret store (B5), exchange memory for one call | Lets anyone exchange codes and refresh tokens as Fides |
| A2 Access token (8 h) | Browser client under its retention mode; Arca in memory | Reads and writes every repository the user and the App installation both reach |
| A3 Refresh token (6 months) | Browser client under its retention mode | Mints new access tokens for six months |
| A4 Authorization code and PKCE verifier | Callback URL, tab session storage, one exchange call | Exchanged for A2 and A3 |
| A5 `state` | Tab session storage | Binds a callback to the tab that started sign-in |
| A6 Application data in GitHub repositories | GitHub, through Arca | The thing the tokens exist to protect |
| A7 Exchange configuration (applications, origins, redirect URIs) | Host configuration | Decides which applications and callbacks are trusted |
| A8 Audit and fault records | Lambda log group, API Gateway access logs, Aegis sink | Detection and incident response; must not hold A1-A5 |
| A9 Release artefacts | GitHub releases, echelon-registry | A tampered client library compromises every consumer |
| A10 Future: organization membership and application roles | Not yet defined (DF-FIDES-2026-0011) | Would decide who may do what inside an application |

## 2. Boundaries

B1 browser, B2 exchange, B3 provider, B4 data repository, B5 secret store
(docs/architecture/TRUST-BOUNDARIES.md section 2), plus:

- **B6 application ↔ application.** Several applications share one exchange
  and, per environment, one GitHub App (docs/hosting/AWS.md: one App per
  environment; GitHub allows ten callback URLs per App).
- **B7 build and release.** CI runners, third-party actions, NuGet feeds,
  the vendored Arca feed and echelon-registry.
- **B8 operator.** The person who holds A1 and A7.

## 3. Threats

| ID | Threat | Assets | Boundary | Mitigation (evidence) | Status | Residual unknowns | Requirements |
|---|---|---|---|---|---|---|---|
| SEC-THR-101 | Login CSRF: attacker's callback lands in the victim's tab | A5, A2 | B1-B3 | Single-use, tab-bound, constant-time-compared `state`; 10-minute lifetime (ClientAcceptanceTests `invalid-state`, `replayed-state`, `expired-state`) | Mitigated | none in Fides | FID-EXC-001, FID-EXC-003, FID-IDN-004 |
| SEC-THR-102 | Authorization-code interception and injection | A4 | B1-B3 | PKCE S256; verifier never leaves the tab before the exchange call (PkceTests, ExchangeAcceptanceTests `wrong-verifier`) | Mitigated (Fides side) | GitHub enforcing PKCE on GitHub App codes is unverified live | FID-EXC-001, FID-TEST-010 |
| SEC-THR-103 | Callback substitution or open redirect delivers a code to an attacker | A4 | B1-B2 | Exact ordinal redirect-URI match per application before any provider call (ConfigurationTests, ExchangeSecurityTests) | Mitigated | GitHub's own callback list is shared by every application on the App (DF-FIDES-2026-0007) | FID-TB-003 |
| SEC-THR-104 | Replayed authorization code | A4 | B2-B3 | Provider single-use codes reported as `code_rejected` (simulated) | Mitigated (simulated) | GitHub single-use behaviour unverified live | FID-EXC-003, FID-TEST-010 |
| SEC-THR-105 | Refresh-token replay or theft and silent reuse | A3 | B1-B2 | Single-flight refresh; adopt newer sibling-tab session on refusal (ClientTests) | **Open** | GitHub rotation and single-use of refresh tokens unverified live; a stateless exchange cannot detect reuse by a thief (DF-FIDES-2026-0012) | FID-SES-006, FID-TEST-011 |
| SEC-THR-106 | Script injection (XSS, malicious dependency) in an application reads tokens | A2, A3 | B1 | Memory-only default retention; 8-hour access tokens | **Open** | Any retention mode is readable by script running in the origin; persistent mode keeps a 6-month refresh token in local storage (DF-FIDES-2026-0015) | FID-CLI-002, FID-SES-008, FID-UX-014 |
| SEC-THR-107 | Cross-application confused deputy: a token issued to application X is refreshed, revoked or used through application Y's registration | A2, A3, A6 | B6 | None: the exchange keys provider clients by provider, not application, and accepts any registered origin's refresh of any refresh token | **Open (violation)** | Severity depends on whether X and Y share data reach (they do today: one App per environment) | FID-DATA-006, FID-AUZ-009 |
| SEC-THR-108 | Cross-tenant data access: a user of tenant T1 reaches T2's repositories | A6 | B4, B6 | GitHub enforces installation and repository permissions | **Unknown** | No tenant model exists; whether tenants map to installations, organizations or repositories is undecided (DF-FIDES-2026-0011, DF-FIDES-2026-0013) | FID-ORG-*, FID-DATA-003..005, FID-TEST-006 |
| SEC-THR-109 | Bypass of application authorization by calling GitHub directly with the user's own token | A6 | B1-B4 | None possible in a browser-held-token architecture: the user holds the token | **Open (architectural)** | Any application permission narrower than the user's GitHub repository permission is advisory (DF-FIDES-2026-0013) | FID-AUZ-004, FID-DATA-001, FID-DATA-002 |
| SEC-THR-110 | Privilege escalation: a user assigns themselves a role or capability | A10 | B1, B4 | None: no role model exists | **Unknown** | Depends on DF-FIDES-2026-0010 | FID-AUZ-006, FID-AUZ-007, FID-TEST-007 |
| SEC-THR-111 | Stale authorization: access removed at GitHub or in the application continues | A2, A6 | B2-B4 | Required-repository check at sign-in only | **Open** | Refresh does not re-check; app-level revocation does not exist | FID-AUZ-008, FID-SES-005 |
| SEC-THR-112 | Clock skew turns an expired token into a "revoked" session, or keeps an expired token in use | A2 | B1 | 5-minute refresh margin | **Open (violation)** | Expiry is the exchange's clock; the client compares with the browser clock; any 401 is treated as revocation | FID-REL-006, FID-REL-007 |
| SEC-THR-113 | Secret or token leaks to logs, errors, URLs, telemetry or Aegis | A1-A5, A8 | B2, B5 | Redacted secret types; fixed refusal bodies; Aegis records exception type only; leak scans (SecretsTests, DiagnosticsTests, FunctionTests) | Mitigated (Fides code) | Platform logs outside Fides (API Gateway, Lambda platform lines) are checked only by template review | FID-EXC-005, FID-OPS-002 |
| SEC-THR-114 | Abuse of the public exchange: credential stuffing of refresh tokens, cost exhaustion, rate-limit pressure on GitHub | A1, A3 | B2 | API Gateway stage throttling (template parameters) | Partially mitigated | `Origin` is not authentication; non-browser callers can forge it; no per-caller limit | FID-OPS-005, FID-OPS-006 |
| SEC-THR-115 | Client secret theft or misuse at the operator | A1 | B5, B8 | Secret by reference; 5-minute cache; one App per environment | Partially mitigated | Rotation is documented but not rehearsed; no alerting on `configuration_unavailable` beyond Aegis fault | FID-OPS-001, FID-OPS-003 |
| SEC-THR-116 | Supply-chain compromise of a Fides release or dependency | A9 | B7 | Sigstore-attested release assets; pinned Arca feed with checksums; pinned Dokimos action | Partially mitigated | No SBOM, no dependency vulnerability scan gate, third-party actions pinned by tag in some workflows | FID-OPS-009, FID-OPS-010 |
| SEC-THR-117 | Provider outage or partial failure yields a partial session or wrongly grants access | A2 | B2-B3 | No partial result; revoke-on-refusal; typed `provider_unavailable` (ExchangeAcceptanceTests `provider-outage`) | Mitigated (simulated) | Live GitHub failure shapes (secondary rate limits, abuse detection) not observed | FID-EXC-004, FID-REL-003 |
| SEC-THR-118 | Sign-out leaves a usable grant (refresh token survives access-token revocation) | A3 | B2-B3 | Client clears every store; exchange revokes the access token | **Unknown** | Whether GitHub's `DELETE /applications/{client_id}/token` invalidates the paired refresh token is unverified | FID-SES-004, FID-TEST-012 |
| SEC-THR-119 | Account takeover through provider-account recovery or login rename | A2, A6 | B3 | Stable numeric subject, `login` display only (DF-FIDES-2026-0007) | Mitigated for rename | GitHub account recovery is GitHub's; Fides cannot detect it | FID-IDN-001, FID-IDN-008 |
| SEC-THR-120 | Service or automation uses a person's token, or a person's token is used as a service identity | A2, A6 | B2-B4 | None defined | **Open** | No service-identity model (DF-FIDES-2026-0014) | FID-SVC-001..004 |
| SEC-THR-121 | Phishing or UI redress on the sign-in and callback screens | A4 | B1 | None: no Fides screens exist | **Open** | Depends on Forma screens and framing policy | FID-UX-004, FID-UX-013 |
| SEC-THR-122 | Malformed or hostile callback query or exchange body | A4, A5 | B1-B2 | Typed decode; 8 KiB limit; JSON content type; unknown fields ignored (ExchangeSecurityTests, conformance suite) | Mitigated | Fuzzing not performed | FID-EXC-001, FID-TEST-008 |
| SEC-THR-123 | Tab-synchronization message forged by same-origin script to sign other tabs out or in | A2 | B1 | Messages carry no token; `signed_in` only triggers a store read | Mitigated (sign-in); accepted (sign-out denial of service from same-origin script) | Same-origin script already owns the tokens (SEC-THR-106) | FID-CLI-003, FID-REL-005 |

## 4. Security invariants

| ID | Invariant | Enforced by | Evidence |
|---|---|---|---|
| SEC-INV-101 | The client secret exists only in the exchange's secret store and process memory | Fides.Hosting, Operator | SecretsTests, TemplateTests |
| SEC-INV-102 | No token, code, verifier or secret appears in a log, error body, Aegis event, URL after the callback, or cross-tab message | Fides.Core, Fides.Hosting, Fides.Client | Leak scans in exchange, adapter and client tests |
| SEC-INV-103 | The exchange returns a whole token response or a typed refusal, never a partial session | Fides.Core | ExchangeAcceptanceTests |
| SEC-INV-104 | Every protected operation is decided by the component that holds the enforcement point, and fails closed (deny) when the decision cannot be made | Application, Arca, GitHub (proposed, FID-AUZ-001..003) | None yet |
| SEC-INV-105 | Tenant identity comes from a trusted context, never from a request parameter (proposed, FID-DATA-003) | Not yet assigned to a component | None yet |
| SEC-INV-106 | A requirement is never reported verified at a level it was not tested at | Fides.Governance | RequirementsCatalogueTests |

## 5. Incident response obligations

See FID-OPS-011 and FID-OPS-012: a documented runbook for client-secret
compromise (rotate the App secret, invalidate the cache, revoke grants),
for a leaked token class, and for a compromised release (yank the registry
entry, publish an advisory). None exists yet.

## 6. Review

Re-review this model when a decision record in section 3 is accepted, when
a second provider or a server-side component is added, and before the
first production deployment.
