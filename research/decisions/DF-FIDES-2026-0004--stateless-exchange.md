---
id: DF-FIDES-2026-0004
title: The code-for-token exchange is stateless, with no session service and no server-held refresh tokens
status: accepted
version: 1.0.0
created: 2026-10-08
updated: 2026-10-08
owners:
  - fides
review_cycle: on-trigger
supersedes: []
superseded_by: []
related_documents:
  - docs/requirements/FIDES-REQUIREMENTS.md
  - research/decisions/DF-FIDES-2026-0001--cloud-agnostic-exchange-aws-first.md
tags: [architecture, exchange, sessions, security]
provenance:
  contributions:
    EXE-20261008T084302418Z-58dc4016:
      operations: [created]
      at: 2026-10-08T08:46:18.872Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "User decision of 2026-10-08 recorded for WI-0003"
---

# DF-FIDES-2026-0004 — Stateless exchange

- **Date:** 2026-10-08
- **Status:** accepted (user decision of 2026-10-08; resolves OQ-FIDES-002)

## Context

The exchange could either keep sessions (holding refresh tokens server-side
and giving the browser an opaque session handle) or stay stateless (passing
the provider's tokens to the browser client and keeping nothing).

## Decision

1. The exchange keeps **no state**: no session store, no database, no cache
   of codes, states or tokens. Each request carries everything it needs.
2. The exchange performs four operations, each a pure function of the
   request, configuration and provider responses: exchange a code, refresh a
   token, revoke a token, and answer CORS preflight.
3. The browser client holds the access and refresh tokens under its
   retention mode (memory-only by default, FID-CLI-002).
4. Replay protection comes from three places, none of which needs server
   state: the client's single-use `state` (consumed on the first callback),
   PKCE (a stolen code is useless without the verifier, which never leaves
   the initiating browser before the exchange call), and the provider's own
   single-use codes, whose refusal the exchange reports as a typed `replay`
   refusal.

## Alternatives considered

- **Session service with server-held refresh tokens.** Allows central
  revocation and keeps refresh tokens out of the browser. Rejected for now:
  it needs a durable store, adds operational surface and cost, and GitHub's
  own revocation endpoint already gives sign-out revocation.

## Consequences

- The exchange scales to zero and runs on Lambda without a datastore.
- A refresh token is exposed to the browser runtime; this is mitigated by
  memory-only default retention and the 8-hour access-token expiry, and is
  recorded in `context/KNOWN-RISKS.md`.
- Central "sign out everywhere" is not available; sign-out revokes the
  current token at GitHub.
