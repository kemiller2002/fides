---
id: DF-FIDES-2026-0015
title: Memory-only stays the default; persistent retention of the refresh token is restricted pending a sealed-cookie design; applications must ship a strict CSP
status: review
version: 0.1.0
created: 2026-11-09
updated: 2026-11-09
owners:
  - fides
review_cycle: on-trigger
supersedes: []
superseded_by: []
related_documents:
  - docs/requirements/FIDES-REQUIREMENTS.md
  - docs/architecture/SECURITY-ARCHITECTURE.md
  - docs/security/THREAT-MODEL.md
tags: [client, tokens, browser, security]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:49.039Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
---

# DF-FIDES-2026-0015 — Browser token retention

- **Date:** 2026-11-09
- **Status:** proposed (`review`); **needs owner approval**. Does not change
  the released 0.2.0 API.
- **Answers:** WI-0022 phase 3 question 6. Requirements: FID-CLI-002,
  FID-SES-008, FID-UX-014.

## Assessment of the released modes

| Mode | Where | Survives | Exposure to injected script | Assessment |
|---|---|---|---|---|
| Memory-only (default) | WASM heap | Until reload | Readable while the page runs | Adequate with a strict CSP; best available without a server |
| Session-scoped | `sessionStorage` | Tab lifetime | Readable; survives reloads in the tab | Acceptable with CSP |
| Persistent (consent) | `localStorage` | Six months (refresh token) | Readable; survives restarts; one injection yields six months of repository access | **Not adequate** for the refresh token |

No browser storage mode protects a token from script running in the
application's origin. The refresh token's six-month lifetime, not the
access token's eight hours, is what makes persistence dangerous.

## Options

1. **Restrict persistent mode** (recommended now): keep the 0.2.0 API, but
   applications must not enable persistence by default, and the client
   guide states the risk; a later major version removes or replaces it.
2. **Sealed refresh cookie** (recommended next): the exchange returns the
   refresh token inside an encrypted, `HttpOnly`, `Secure`,
   `SameSite=Strict` cookie scoped to the exchange's own path. The exchange
   stays stateless (no store; the key is a secret like the client secret);
   page script never sees the refresh token. Requires the exchange to be
   same-site with the application (for example `auth.<app domain>`) because
   browsers block third-party cookies, and a versioned protocol (`/v2`).
3. **Backend-for-frontend** (DF-FIDES-2026-0012 option 3).
4. **Keep as is.** Rejected: accepts SEC-THR-106 at six-month scale.

## Recommendation

Accept option 1 now with FID-UX-014 (strict CSP, Trusted Types) as a
condition of using Fides, and design option 2 as Fides 1.0's protocol
change. Any change to released behaviour goes through a versioned release.
