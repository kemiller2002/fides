---
id: DF-FIDES-2026-0012
title: The stateless exchange is kept; its replay and revocation limits are stated, with the conditions that would supersede DF-FIDES-2026-0004
status: accepted
version: 1.0.0
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
tags: [stateless, replay, revocation, security]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:47.788Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
    EXE-20261109T154841724Z-a435eb94:
      operations: [modified]
      at: 2026-11-09T15:50:39.670Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Record the owner's acceptance of DF-FIDES-2026-0010..0015 on 2026-11-09 (WI-0023)"
---

# DF-FIDES-2026-0012 — What a stateless exchange can and cannot guarantee

- **Date:** 2026-11-09
- **Status:** accepted (owner decision of 2026-11-09, recommendation adopted). Preserves
  DF-FIDES-2026-0004 (accepted).
- **Answers:** WI-0022 phase 3 question 3. Requirements: FID-SES-004,
  FID-SES-006, FID-SES-010, FID-TEST-010..013.

## Analysis

| Guarantee | Satisfied statelessly? | How, and what is unverified |
|---|---|---|
| State replay (login CSRF) | Yes | Client single-use, tab-bound state (verified, unit and simulated) |
| Code interception or replay | Yes, if GitHub enforces PKCE and single use | PKCE S256 plus provider single-use codes; **GitHub's enforcement unverified live** (FID-TEST-010) |
| Refresh-token replay by the legitimate client | Yes | Single-flight refresh and sibling adoption (verified, unit) |
| Detecting a **stolen** refresh token being used by a thief | **No** | Reuse detection needs a record of issued tokens; GitHub's own rotation behaviour is unverified (FID-TEST-011) |
| Sign-out leaves no usable grant | **Unknown** | Access token is revoked; whether the refresh token dies with it is unverified (FID-TEST-012) |
| Revoke every session of a person ("everywhere") | Partly | GitHub's grant revocation revokes all tokens of the person for the App, but across every application sharing that App (FID-SES-010) |
| Administrator revokes one application session of another person | **No** | No session list exists |
| Re-check authorization on refresh | Yes | The refresh request can re-run the required-repository check (FID-AUZ-008, missing today) |

## Options

1. **Keep stateless (recommended)** and close the gaps statelessly where
   possible: live-verify GitHub's guarantees, use grant revocation on
   sign-out if access-token revocation leaves the refresh token alive, and
   re-check access on refresh.
2. **Add a minimal state store** (issued-refresh-token families for reuse
   detection, a revocation list). Gains theft detection and administrative
   revocation; costs a datastore, a consistency model and a larger attack
   surface holding nothing secret but still security-relevant.
3. **Backend-for-frontend sessions** (server-held refresh tokens, HttpOnly
   cookie). Strongest browser posture; supersedes DF-FIDES-2026-0004 fully.

## Recommendation

Keep option 1. Supersede DF-FIDES-2026-0004 with option 2 or 3 only if the
owner requires stolen-refresh-token detection or administrator revocation
of individual sessions, or if FID-TEST-012 shows sign-out cannot be made
complete statelessly. DF-FIDES-2026-0015's sealed-cookie option keeps the
exchange stateless while removing the refresh token from script.
