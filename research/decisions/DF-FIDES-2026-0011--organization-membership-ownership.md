---
id: DF-FIDES-2026-0011
title: Organization membership is not held by Fides; tenants map to GitHub App installations and Arca repositories
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
tags: [organizations, tenancy, architecture]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:47.416Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
    EXE-20261109T154841724Z-a435eb94:
      operations: [modified]
      at: 2026-11-09T15:50:39.286Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Record the owner's acceptance of DF-FIDES-2026-0010..0015 on 2026-11-09 (WI-0023)"
---

# DF-FIDES-2026-0011 — Organization membership ownership

- **Date:** 2026-11-09
- **Status:** accepted (owner decision of 2026-11-09, recommendation adopted).
- **Answers:** WI-0022 phase 3 question 2. Requirements: FID-ORG-001..007,
  FID-DATA-003..004.

## Context

Applications will serve several organizations, and a person may belong to
several. Fides is stateless (DF-FIDES-2026-0004) and holds no data store.
Arca already defines organization sub-namespaces with immutable ids and
says an organization must be able to live in a different repository
(ARCA-LOC-005), with one repository per permission boundary (ARCA-LOC-004).
GitHub already has two membership-like facts: organization membership and
which accounts and repositories a GitHub App installation covers.

## Options

| Option | For | Against |
|---|---|---|
| A. Fides holds memberships | One place to ask | Needs a datastore, contradicting DF-FIDES-2026-0004; duplicates GitHub's own access facts |
| B. Separate shared membership service | Clean ownership; server-side enforcement possible | New component and datastore; nothing needs it yet |
| C. Membership is derived (recommended) | **Tenant = a GitHub App installation plus the Arca repositories configured for it**; data reach is enforced by GitHub; application-level membership and roles live in an administrators-only Arca repository per tenant | No new service; the enforced boundary (repository permission) and the tenant boundary coincide | Membership listing needs GitHub API calls; application roles stored in GitHub inherit FID-AUZ-004's limits |
| D. GitHub organization membership alone | Simple | Personal accounts have no organization; organization membership does not imply App installation reach |

## Recommendation

Adopt **C**, and defer the membership workflows (invitation, delegation)
until a consumer needs more than one organization. Keep **B** as the
supersession path if a server-side enforcement point is approved under
DF-FIDES-2026-0010.

## Risks

- One GitHub App shared by tenants would let installation reach cross
  tenants that share a person. DF-FIDES-2026-0013 and FID-DATA-006 address
  the App-per-domain split.
- Tenant context from URLs. FID-DATA-003 and FID-ORG-007 forbid it.
