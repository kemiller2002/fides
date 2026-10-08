---
id: FIDES-TRUST-BOUNDARIES
title: Fides trust-boundary model
status: accepted
version: 1.0.0
created: 2026-10-08
updated: 2026-10-08
owners:
  - fides
related_documents:
  - docs/requirements/FIDES-REQUIREMENTS.md
  - research/decisions/DF-FIDES-2026-0003--github-app-user-to-server-tokens.md
  - research/decisions/DF-FIDES-2026-0004--stateless-exchange.md
  - research/decisions/DF-FIDES-2026-0007--what-github-proves-and-what-fides-owns.md
tags: [security, trust-boundaries, oauth, github]
provenance:
  contributions:
    EXE-20261008T084724760Z-06b91672:
      operations: [created]
      at: 2026-10-08T08:48:15.505Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "FID-TB-001..005 trust-boundary model (issue #1)"
---

# Fides trust-boundary model

This model answers FID-TB-001..006. Every rule that code enforces names the
test family that proves it; the tests themselves land with the slices named.

## 1. Users (FID-TB-001)

| Actor | Who | Trusted for |
|---|---|---|
| Person | Someone signing in to an Echelon application with a GitHub account | Nothing until GitHub has authenticated them; then only as the identity GitHub reports |
| Application | An Echelon web application (Chrona, Summa, Signal) registered with Fides as an OAuth client | Its registered origins and redirect URIs, and nothing it claims at request time |
| Exchange | Fides's stateless server-side service | Holding the client secret and talking to GitHub's token and revocation endpoints |
| Provider | GitHub, through the operator's GitHub App | Authenticating the person, issuing and refreshing tokens, reporting the identity, deciding repository reach |
| Arca | The shared data layer inside the application | Using the token it is handed; it never acquires, refreshes or stores tokens |
| Operator | The repository owner who registers the GitHub App and deploys the exchange | Configuration and secrets |

## 2. Trust boundaries (FID-TB-001)

```
   B1 browser ─────────────── B2 exchange ─────────────── B3 provider
   ┌─────────────────────┐    ┌──────────────────────┐    ┌──────────────────┐
   │ application + Fides │    │ hosting adapter      │    │ github.com       │
   │ client + Arca       │◀──▶│ pure exchange core   │◀──▶│ api.github.com   │
   └─────────────────────┘    │ client secret (B5)   │    └──────────────────┘
            │                 └──────────────────────┘             │
            └───────────── top-level navigation (authorize) ───────┘
                                                                    │
                              B4 data repository (GitHub) ◀─────────┘ via Arca
```

| Boundary | What crosses | What must hold |
|---|---|---|
| B1 browser ↔ B2 exchange | Code, PKCE verifier, redirect URI and application id in; tokens and identity out | HTTPS only; the request's `Origin` is a registered origin of the named application; the exchange trusts nothing in the body until validated |
| B1 browser ↔ B3 provider | Top-level navigation to the authorize URL; the callback with `code` and `state` | `state` is single-use and bound to the tab that started sign-in; PKCE S256 binds the code to that tab's verifier |
| B2 exchange ↔ B3 provider | Client id and secret with the code or refresh token; user token to `GET /user` | Fixed provider endpoints from configuration, never from the request; responses are untrusted input, parsed into typed results |
| B1 browser ↔ B4 repository | Arca's API calls with the user token | GitHub enforces the App's permissions and installations; Arca enforces nothing on Fides's behalf |
| B2 exchange ↔ B5 secret store | The client secret by reference | Read at runtime from the operator's secret store; never in configuration files, environment values, logs or the repository |

## 3. Token and session ownership (FID-TB-002)

| Material | Created by | Held by | Lifetime | Who may read it |
|---|---|---|---|---|
| Client secret | Operator (GitHub App settings) | Exchange's secret store only | Until the operator rotates it | The exchange process, at the moment of a provider call |
| `state` | Client | Client, per tab, until consumed | One callback, at most 10 minutes | The client only |
| PKCE verifier | Client | Client, per tab, until the exchange call | One exchange call, at most 10 minutes | The client and, once, the exchange |
| Authorization code | Provider | In the callback URL until the client removes it, then in one exchange call | Single use, minutes (provider-defined) | The client and the exchange |
| Access token | Provider | Client session, under its retention mode | 8 hours (`expires_in`) | The client and, through the token provider, Arca |
| Refresh token | Provider | Client session, under its retention mode | 6 months (`refresh_token_expires_in`), rotated on every refresh | The client; sent only to the exchange's refresh and revoke endpoints |
| Identity (login, id, name) | Provider (`GET /user`) | Client session | As long as the session | The application |

The exchange keeps none of these after the request ends (DF-FIDES-2026-0004).
Retention modes (FID-CLI-002): **memory-only** by default (gone on reload);
**session-scoped** (per tab, cleared when the tab closes); **persistent**
only by explicit user choice with disclosure and a clear action.

## 4. Callback and redirect behaviour (FID-TB-003)

- Each application registers its exact redirect URIs per environment. The
  exchange compares the request's redirect URI to the registered list by
  **exact, ordinal string equality**: no prefix, wildcard, case folding,
  trailing-slash or query tolerance. A mismatch is the `redirect_uri_not_allowed`
  refusal and happens before any provider call.
- A redirect URI must be absolute `https`, with no fragment and no userinfo;
  `http://localhost` and `http://127.0.0.1` are allowed only in an
  environment configuration that explicitly enables loopback (test).
- The callback page consumes `state` before anything else. A missing,
  unknown, expired or already-used `state` is refused without calling the
  exchange; the stored verifier is discarded either way.
- After the callback the client removes `code` and `state` from the address
  bar (history replacement), so neither survives in history, bookmarks or
  the `Referer` header.
- The exchange answers CORS only for the application's registered origins
  (FID-HOST-005); a disallowed origin gets no `Access-Control-Allow-Origin`
  header and the request is refused.

## 5. Sign-in versus repository authorization (FID-TB-004)

| Layer | Decides | Owned by |
|---|---|---|
| Authentication | Who the person is: the GitHub account behind the token | Provider, reported through Fides |
| Repository reach | Which repositories the token can read or write | GitHub: the App's declared permissions, where the operator installed it, and the person's own access |
| Application authorization | What the person may do in the application | Each application's capability model |

Fides establishes identity only. An application may register a **required
repository**; the exchange then checks the new token can read it and refuses
sign-in with `repository_access_denied` otherwise, revoking the unusable
token. Fides never widens or narrows what the token itself can reach.

## 6. Logout and revocation (FID-TB-005)

- **Sign-out** clears the token from every client store (memory, session
  and persistent) and asks the exchange to revoke it at GitHub
  (`DELETE /applications/{client_id}/token`). Local clearing happens first
  and does not wait on the network; a failed revocation is reported, never
  retried silently with the token kept.
- **Expired**: the access token is past `expires_in` (less a clock-skew
  margin). The client refreshes through the exchange; if refresh fails, the
  session becomes `expired`.
- **Revoked**: GitHub answers `401` for the token, or refresh reports the
  grant invalid. The session becomes `revoked` and the tokens are cleared.
- Applications and Arca see these as typed states (signed out, signing in,
  signed in, expired, revoked, provider unavailable), never as exceptions.
- Other tabs are told through a cross-tab signal (FID-CLI-003), but they
  still confirm with the provider before acting.

## 7. What GitHub proves and what Fides owns (FID-TB-006)

Recorded in DF-FIDES-2026-0007.

## 8. Hosting constraints

- The exchange is stateless and holds no data store (DF-FIDES-2026-0004).
- It reads its configuration and the client secret's location from the
  host's configuration; the secret itself is read from the secret store.
- It writes no request or response bodies to logs. Log lines carry only the
  operation, the application id, the outcome code and the duration.
- The account, region and domain are operator configuration
  (DF-FIDES-2026-0005).

## 9. Threats and the tests that answer them

| Threat | Mitigation | Tests (slice) |
|---|---|---|
| Forged or replayed callback (CSRF login) | Single-use, tab-bound `state` | Client state tests (WI-0009); acceptance `invalid-state`, `replayed-state` (WI-0005) |
| Intercepted code used elsewhere | PKCE S256; verifier never leaves the tab before the exchange call | Exchange PKCE validation (WI-0007); acceptance `wrong-verifier` |
| Code delivered to an attacker's callback | Exact redirect-URI allow-list | Redirect allow-list tests (WI-0007); acceptance `wrong-callback` |
| Replayed code | Provider single-use codes reported as `replay` | Acceptance `replayed-code` |
| Cross-origin call to the exchange | CORS allow-list and origin check | Exchange origin tests (WI-0007); adapter conformance (WI-0010) |
| Token, code, secret or verifier leaked to logs, errors or URLs | Redacted secret types; fixed refusal bodies | Leak scans over every response and log in the exchange, adapter and client tests |
| Use of a revoked or expired token | Typed `revoked`/`expired` states; refresh | Acceptance `revoked-identity`, `expired-session` |
| Signed-in user without access to the application's data | Optional required-repository check | Acceptance `repository-access-denied` |
| Provider outage mid sign-in | Typed `provider_unavailable`, no partial session | Acceptance `provider-outage` |
