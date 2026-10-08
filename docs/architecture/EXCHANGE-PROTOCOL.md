---
id: FIDES-EXCHANGE-PROTOCOL
title: Fides exchange and client protocol
status: accepted
version: 1.0.0
created: 2026-10-08
updated: 2026-10-08
owners:
  - fides
related_documents:
  - docs/architecture/TRUST-BOUNDARIES.md
  - docs/requirements/FIDES-REQUIREMENTS.md
tags: [protocol, exchange, client, oauth]
provenance:
  contributions:
    EXE-20261008T085052343Z-fb043497:
      operations: [created]
      at: 2026-10-08T08:56:08.794Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Public exchange and client contract the acceptance scenarios target (FID-TEST-001)"
---

# Fides exchange and client protocol

This is the public contract between the Fides client (in the browser), the
Fides exchange (server side) and the applications. The acceptance tests
(`tests/Fides.Tests/Acceptance`) are written against it, not against
internal types, so the implementation can change without the tests moving.

## 1. Sign-in sequence

1. The client generates a `state` (32 random bytes, base64url) and a PKCE
   `code_verifier` (32 random bytes, base64url, 43 characters), stores both
   with the creation time for this tab, and navigates to the provider's
   authorize URL with `client_id`, `redirect_uri`, `state`,
   `code_challenge = BASE64URL(SHA256(code_verifier))` and
   `code_challenge_method=S256`.
2. The provider redirects to `redirect_uri?code=...&state=...` (or
   `?error=access_denied&state=...` when the person declines).
3. The client consumes the stored pending sign-in (single use), checks the
   state, removes `code` and `state` from the address bar, and calls
   `POST /v1/token`.
4. The exchange validates, calls the provider, resolves the identity, checks
   the required repository if the application registered one, and returns
   the tokens and identity.

## 2. Exchange endpoints

All requests are `application/json` bodies of at most 8 KiB, sent with the
browser's `Origin` header. Unknown fields are ignored; missing or wrongly
typed required fields are `malformed_request`.

| Method and path | Body | Success |
|---|---|---|
| `POST /v1/token` | `application`, `code`, `codeVerifier`, `redirectUri` | `200` with tokens and `identity` |
| `POST /v1/refresh` | `application`, `refreshToken` | `200` with tokens |
| `POST /v1/revoke` | `application`, `accessToken` | `204`, no body |
| `OPTIONS` any of the above | none | `204` with CORS headers for a registered origin |

A token response is:

```json
{
  "accessToken": "…",
  "accessTokenExpiresAt": "2026-10-08T17:00:00+00:00",
  "refreshToken": "…",
  "refreshTokenExpiresAt": "2027-04-08T09:00:00+00:00",
  "identity": { "provider": "github", "subject": "583231", "login": "octocat", "name": "The Octocat" }
}
```

`identity` is present on `/v1/token` only. `subject` is the provider's
stable numeric account id; `login` is a display value.

## 3. Refusals

A refusal is `{"error": "<code>"}` with the status below and nothing else:
no description, no echo of the request, no provider text. The codes are the
contract.

| Code | Status | Meaning | Provider called? |
|---|---|---|---|
| `malformed_request` | 400 | Body is not JSON, is too large, or lacks a required field | no |
| `unknown_application` | 400 | `application` is not registered | no |
| `origin_not_allowed` | 403 | `Origin` is missing or not registered for the application | no |
| `redirect_uri_not_allowed` | 400 | `redirectUri` is not exactly one of the application's registered URIs | no |
| `invalid_code_verifier` | 400 | `codeVerifier` is not 43-128 characters of `A-Z a-z 0-9 - . _ ~` | no |
| `configuration_unavailable` | 500 | The client secret could not be read | no |
| `code_rejected` | 400 | The provider refused the code: wrong, expired, already used (replayed) or not matching the verifier | yes |
| `refresh_rejected` | 401 | The provider refused the refresh token: revoked, expired or already used | yes |
| `identity_revoked` | 401 | The provider refused the new token when resolving the identity | yes |
| `repository_access_denied` | 403 | The application's required repository is not readable with the new token; the token was revoked and not returned | yes |
| `provider_unavailable` | 503 | The provider failed, timed out or rate-limited; nothing was issued to the client | yes |
| `provider_contract_violation` | 502 | The provider answered in a shape Fides does not accept, including a token without expiry | yes |
| `not_found` | 404 | Unknown path | no |
| `method_not_allowed` | 405 | Known path, wrong method | no |

The exchange never returns a partial result: either the whole token
response or a refusal.

## 4. CORS

For a registered origin of the named application (preflight: any registered
origin of any application, since the preflight carries no body), the
exchange answers with `Access-Control-Allow-Origin: <that origin>`,
`Vary: Origin`, `Access-Control-Allow-Methods: POST, OPTIONS`,
`Access-Control-Allow-Headers: content-type` and
`Access-Control-Max-Age: 600`. It never answers `*` and never allows
credentials. An unregistered origin gets no CORS headers.

## 5. Client callback outcomes

| Outcome | When | Exchange called? |
|---|---|---|
| `state_invalid` | No pending sign-in in this tab, a different `state`, a missing `state`, or a pending sign-in already consumed (replayed callback) | no |
| `state_expired` | The pending sign-in is older than 10 minutes | no |
| `provider_denied` | The callback carries `error` (for example `access_denied`) | no |
| `signed_in` | The exchange returned tokens | yes |
| any exchange refusal code | The exchange refused | yes |

The pending sign-in is removed on every outcome.

## 6. Session states and the token provider

Session states: `signed_out`, `signing_in`, `signed_in`, `expired`,
`revoked`, `provider_unavailable`.

The token provider, which Arca consumes (ARCA-AUTH-001), yields either a
current access token or one of three typed failures:

| Result | When |
|---|---|
| token | Signed in and the access token is valid for at least the refresh margin (5 minutes), refreshing first if needed |
| `none` | Signed out or signing in |
| `expired` | The access token expired and could not be refreshed: the refresh token is past its expiry, or the provider was unavailable |
| `revoked` | The provider refused the token or the refresh grant before its expiry, or the application reported a `401` for the token |
