---
id: DF-FIDES-2026-0003
title: The GitHub provider is a GitHub App using expiring user-to-server tokens with refresh, not an OAuth App
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
  - research/decisions/DF-FIDES-2026-0002--github-only-extensible-provider-model.md
tags: [providers, github, github-app, tokens, security]
provenance:
  contributions:
    EXE-20261008T084302418Z-58dc4016:
      operations: [created]
      at: 2026-10-08T08:46:18.478Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "User decision of 2026-10-08 recorded for WI-0003"
---

# DF-FIDES-2026-0003 — GitHub App with expiring user-to-server tokens

- **Date:** 2026-10-08
- **Status:** accepted (user decision of 2026-10-08; resolves OQ-FIDES-001)

## Context

GitHub offers two ways for a web application to act for a user. An **OAuth
App** gets a token whose reach is set by coarse scopes (`repo` grants every
repository the user can reach) and which does not expire. A **GitHub App**
gets a *user-to-server* token whose reach is the intersection of the App's
declared fine-grained permissions, the repositories the App is installed on,
and what the user can access. With "expire user authorization tokens"
enabled, the access token expires after 8 hours and comes with a refresh
token that expires after 6 months; refreshing needs the App's client secret.

Arca keeps each permission boundary in its own repository, so the narrower
per-installation grant fits it (FID-PRV-002, SIG ADM-004).

## Decision

1. Fides's GitHub provider is a **GitHub App**. OAuth Apps are not supported.
2. Fides uses **user-to-server tokens with expiry and refresh**. The App
   registration must have token expiration enabled; Fides treats a token
   response without `expires_in` and `refresh_token` as a provider contract
   violation, not as a non-expiring token.
3. Authorization uses the authorization-code flow with **PKCE (S256)** and a
   single-use `state`, against `https://github.com/login/oauth/authorize`
   and `https://github.com/login/oauth/access_token`.
4. Refresh (`grant_type=refresh_token`) and revocation
   (`DELETE /applications/{client_id}/token`) go through the exchange,
   because both need the client secret.
5. Identity comes from `GET /user` with the user token (FID-PRV-003).

## Alternatives considered

- **OAuth App with `repo` scope.** Simpler registration, but the grant covers
  every repository the user can reach and the token never expires. Rejected
  for over-broad reach.
- **GitHub App without token expiry.** Avoids refresh, but a stolen token
  lives until revoked. Rejected.

## Consequences

- Repository reach is controlled by where the operator installs the App and
  by the App's declared permissions (FID-TB-004).
- The client must refresh before expiry; the exchange gains a refresh
  endpoint.
- The operator registers one GitHub App per environment (FID-HOST-003); its
  fields are listed in the hosting documentation.
