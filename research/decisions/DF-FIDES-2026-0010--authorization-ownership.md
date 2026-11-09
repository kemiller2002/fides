---
id: DF-FIDES-2026-0010
title: Fides stays authentication-only; authorization is owned by the enforcement point; no central authorization service
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
tags: [authorization, security, architecture]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:46.981Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
---

# DF-FIDES-2026-0010 — Authorization ownership

- **Date:** 2026-11-09
- **Status:** proposed (status `review`); **needs owner approval**. Does not
  supersede any accepted record.
- **Answers:** WI-0022 phase 3 question 1. Requirements: FID-AUZ-001..007.

## Context

Fides authenticates (DF-FIDES-2026-0007). Arca states that authentication
is separate from authorization and that each application enforces its own
capabilities (ARCA-AUTH-005). Limen states that route guards are not an
authorization boundary. Consumers (Chrona, Summa, Signal) need
capabilities, roles and resource-level decisions, and the mission asks
whether Fides should also offer a reusable authorization policy engine.

The decisive fact is where enforcement can happen. Today every protected
data operation is a GitHub API call made from the browser with the user's
own token (DF-FIDES-2026-0013). The only enforcement points are GitHub's
repository permissions and, in future, a server-side component that holds
its own identity.

## Options

| Option | What it means | For | Against |
|---|---|---|---|
| A. Authentication-only (recommended now) | Fides authenticates and reports identity; each enforcement point authorizes | Matches the current architecture and ARCA-AUTH-005; nothing to bypass because nothing is claimed | Each application writes its own decision code; risk of inconsistent vocabularies |
| B. Fides ships a pure policy library | A small F# package: `Decision = Permit | Deny of reason`, default-deny combinators, `(principal, action, resource, tenant)` inputs | Shared vocabulary, pure and testable, no service | In the browser it decides nothing the user cannot bypass; it can create false confidence; widens Fides's scope |
| C. Separate shared authorization component | A new Echelon component owns the vocabulary and, later, a server-side enforcement point | Clean ownership; can run server-side | New component, new release train; premature with no server-side consumer |
| D. Central authorization service | A hosted policy decision point every application calls | Central revocation and audit | Contradicts the stateless exchange; needs a datastore; the browser still holds the GitHub token, so data access bypasses it anyway |

## Recommendation

Adopt **A now**. Record the decision vocabulary as requirements
(FID-AUZ-003, FID-AUZ-005) so it can be shared, and revisit **C** when the
first server-side enforcement point is approved. Reject **D**: it adds a
datastore and cannot see data access made directly with the user's token.
Do not adopt **B** inside Fides: a browser-side policy engine shipped by the
security boundary would be read as enforcement.

## Risks

- Applications drift into inconsistent authorization models. Mitigation:
  FID-AUZ-002 requires each application to list its protected operations and
  enforcement points before production.
- Teams treat UI checks as authorization. Mitigation: FID-AUZ-002 and the
  negative test that calls the enforcement point directly.

## Consequences if accepted

FID-AUZ-003..007 become application or future-component requirements;
Fides keeps FID-AUZ-001, FID-AUZ-008 and FID-AUZ-009 (session-level checks
it does own).
