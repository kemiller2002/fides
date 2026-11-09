---
id: FIDES-AUTHORIZATION-OWNERSHIP
title: Authorization ownership proposal
status: draft
version: 0.1.0
created: 2026-11-09
updated: 2026-11-09
owners:
  - fides
related_documents:
  - research/decisions/DF-FIDES-2026-0010--authorization-ownership.md
  - research/decisions/DF-FIDES-2026-0013--github-backed-storage-permission-and-tenancy.md
  - docs/architecture/SECURITY-ARCHITECTURE.md
tags: [authorization, proposal]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:50.297Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
    EXE-20261109T154841724Z-a435eb94:
      operations: [modified]
      at: 2026-11-09T15:50:43.398Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Record the owner's acceptance of DF-FIDES-2026-0010..0015 on 2026-11-09 (WI-0023)"
---

# Authorization ownership proposal

**Accepted by the owner on 2026-11-09** (DF-FIDES-2026-0010). No
authorization service or policy engine is implemented or planned under
this decision.

## 1. The rule

> The component that holds the enforcement point owns the decision.
> Fides authenticates. It does not authorize application actions.

## 2. Ownership table

| Decision kind | Owner | Enforcement point | Bypassable by the user? | Requirements |
|---|---|---|---|---|
| Identity (who) | Fides, reporting GitHub | GitHub authentication | No | FID-IDN-001..002 |
| Application may use Fides | Fides exchange | Exchange configuration | No | FID-TB-003, FID-HOST-005 |
| Session still valid | Fides client and exchange | Refresh at the exchange; GitHub grant | No | FID-SES-*, FID-AUZ-008 |
| Repository read and write | GitHub | GitHub API | No | FID-TB-004, FID-DATA-002 |
| Application capability **coarser than or equal to** a repository | Application, mapped to repository layout | GitHub API | No, if mapped to separate repositories | FID-AUZ-004, FID-DATA-004 |
| Application capability **finer than** a repository | Application | None in the browser | **Yes** — advisory unless a server-side point exists | FID-AUZ-004 |
| Role and membership assignment | Application per tenant (DF-FIDES-2026-0011) | Administrators-only repository | No, if stored in a separate repository | FID-AUZ-006..007, FID-ORG-* |
| Service action | Service | Installation token scope | n/a | FID-SVC-* |

## 3. What each application must deliver before production

1. A list of protected operations, each with its enforcement point and its
   classification: repository-enforced, server-enforced or advisory
   (FID-AUZ-002, FID-AUZ-004).
2. A data layout where each permission boundary is its own repository
   (ARCA-LOC-004).
3. Default-deny decision code with resource-level inputs (FID-AUZ-003,
   FID-AUZ-005).
4. A negative test per protected operation that calls the enforcement
   point directly, not through the UI.

## 4. Shared vocabulary

To keep applications consistent without a service, the decision shape is
specified here as a contract for applications to adopt, not as a Fides
package:

```fsharp
type Decision =
    | Permit
    | Deny of reason: DenyReason

and DenyReason =
    | Unauthenticated          // no or invalid session: show sign-in
    | NotPermitted             // signed in, not allowed: show denial
    | DecisionUnavailable      // an input is missing: deny, show outage

/// Inputs are always all four; any unknown input is Deny DecisionUnavailable.
type Request = { Principal: string; Action: string; Resource: string; Tenant: string }
```

The three `DenyReason` cases are the three UI classes of FID-UX-010.

## 5. Alternatives

See DF-FIDES-2026-0010: a Fides policy library, a separate authorization
component, and a central authorization service, with reasons.
