---
id: DF-FIDES-2026-0007
title: GitHub authentication proves control of a GitHub account and the token's grant; Fides owns client registration, redirect policy, state and PKCE, session states and revocation propagation
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
  - docs/architecture/TRUST-BOUNDARIES.md
  - docs/requirements/FIDES-REQUIREMENTS.md
  - research/decisions/DF-FIDES-2026-0003--github-app-user-to-server-tokens.md
tags: [security, trust-boundaries, github, identity]
provenance:
  contributions:
    EXE-20261008T084724760Z-06b91672:
      operations: [created]
      at: 2026-10-08T08:48:15.068Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "FID-TB-006: what GitHub authentication proves versus what Fides owns (issue #1)"
---

# DF-FIDES-2026-0007 — What GitHub proves, what Fides owns

- **Date:** 2026-10-08
- **Status:** accepted (answers FID-TB-006 and issue #1)

## Context

Issue #1 asks for a record of what GitHub authentication proves versus what
Fides must own. Getting this wrong in either direction is a security defect:
trusting GitHub for something it does not prove (for example, that the
callback reached the right application) or re-implementing something GitHub
already enforces (for example, repository permissions).

## Decision

**GitHub proves**, for a user-to-server token obtained through the GitHub
App's authorization-code flow:

1. The person controlled a GitHub account at authorization time, under
   GitHub's own authentication (password, two-factor, passkeys, SSO).
2. The person authorized this GitHub App.
3. The token's reach: the App's declared permissions, intersected with the
   installations the operator made and the person's own access.
4. The account's identity, as reported by `GET /user` for that token
   (numeric `id`, which is stable, and `login`, which can change).
5. When asked, that a token or refresh grant is still valid.

**GitHub does not prove**, and Fides must own:

1. **Client registration**: which applications may use Fides, their allowed
   origins and their exact redirect URIs.
2. **Redirect policy**: exact matching before any provider call. GitHub
   checks the redirect URI against the App's callback list, but that list is
   shared by every application using the App, so it cannot tell one
   application's callback from another's.
3. **Request binding**: single-use `state` and PKCE verifier generation,
   storage and validation in the client.
4. **Session states**: signed out, signing in, signed in, expired, revoked
   and provider unavailable, and how applications and Arca see them.
5. **Revocation propagation**: clearing every client store on sign-out and
   on revocation, revoking at GitHub, and signalling other tabs.
6. **Secret custody**: the client secret stays in the exchange's runtime
   secret store and never reaches a browser, log or repository.

Fides uses the numeric GitHub user `id` as the stable identity key and treats
`login` as a display value.

## Consequences

- Repository authorization is never re-implemented in Fides (FID-TB-004).
- A second provider must state its own "proves" list before it is added
  (FID-PRV-001).
