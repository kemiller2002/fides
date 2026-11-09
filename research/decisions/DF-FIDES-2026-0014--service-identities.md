---
id: DF-FIDES-2026-0014
title: Non-human identities use GitHub App installation tokens minted in their own runtime, never a person's token and never the browser-facing exchange
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
tags: [service-identity, github, security]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:48.659Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
    EXE-20261109T154841724Z-a435eb94:
      operations: [modified]
      at: 2026-11-09T15:50:40.460Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Record the owner's acceptance of DF-FIDES-2026-0010..0015 on 2026-11-09 (WI-0023)"
---

# DF-FIDES-2026-0014 — Service identities

- **Date:** 2026-11-09
- **Status:** accepted (owner decision of 2026-11-09, recommendation adopted).
- **Answers:** WI-0022 phase 3 question 5. Requirements: FID-SVC-001..004.

## Context

No Echelon component defines service identity. Arca 0.7.0 models
`IdentityKind = User | Bot | Installation`. Background work (imports,
scheduled jobs, CI, a future server-side enforcement point) must not borrow
a person's user-to-server token: the token expires, carries the person's
reach, and attributes the action to the person.

## Options

| Option | For | Against |
|---|---|---|
| A. Installation tokens minted in the service's runtime (recommended) | GitHub's native non-human identity; 1-hour tokens; scoped to repositories and permissions per request; private key stays with the service | Each service holds an App private key in its own secret store |
| B. Mint installation tokens in the Fides exchange | One place for keys | The exchange is public; it would become an installation-token oracle for any caller that passes its checks; rejected |
| C. Workload identity federation (GitHub Actions OIDC, AWS IAM) to a token broker | No long-lived key | Needs a broker service; more moving parts; reasonable later step |
| D. Personal access tokens or a bot user | Easy | Long-lived, broad, tied to a human or a shared account; rejected |

## Recommendation

Adopt **A** for any service that acts on repositories, as a separate
mechanism from user sign-in. Fides defines the principal kinds and the
service contract (FID-SVC-001..004); it does not host the private key.
Consider **C** when more than one service needs keys.
