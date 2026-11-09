---
id: FIDES-ORG-TENANT
title: Organization and tenant-isolation requirements
status: draft
version: 0.1.0
created: 2026-11-09
updated: 2026-11-09
owners:
  - fides
related_documents:
  - docs/requirements/FIDES-REQUIREMENTS.md
  - research/decisions/DF-FIDES-2026-0011--organization-membership-ownership.md
  - research/decisions/DF-FIDES-2026-0013--github-backed-storage-permission-and-tenancy.md
tags: [organizations, tenancy, isolation]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:51.875Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
---

# Organization and tenant isolation

The normative requirements are in the catalogue: FID-ORG-001..007,
FID-DATA-003..006, FID-AUZ-006..009 and FID-TEST-006..007. This document
explains the model they assume. **Everything here depends on
DF-FIDES-2026-0011 and DF-FIDES-2026-0013, which are proposed, not
accepted**, so every FID-ORG requirement is `blocked`.

## 1. Terms

| Term | Meaning in the proposed model |
|---|---|
| Identity | A provider account: `github:<numeric id>` (FID-IDN-001, FID-IDN-003) |
| Organization | A customer of an application, with an immutable id (FID-ORG-001) |
| Tenant | The data-isolation unit: one organization's repositories, reached through one GitHub App installation |
| Membership | A principal's relation to an organization, held separately from the identity (FID-ORG-002) |
| Isolation domain | A set of applications allowed to share one GitHub App and therefore token reach (FID-DATA-006) |

## 2. Isolation layers, outermost first

1. **Environment.** Test, staging and production use separate GitHub Apps,
   secrets and repositories (FID-HOST-003, ARCA-LOC-007).
2. **Isolation domain.** One GitHub App per domain; a token from one
   domain cannot reach another's repositories (FID-DATA-006). *Violated
   today*: one App per environment serves every application.
3. **Tenant.** One installation and its repositories per organization;
   nothing shared between tenants (FID-DATA-004, ARCA-LOC-005).
4. **Permission boundary inside a tenant.** One repository per boundary,
   for example an administrators-only repository for roles and membership
   (ARCA-LOC-004, FID-AUZ-006).
5. **Application capability.** Repository-enforced, server-enforced or
   advisory (FID-AUZ-004).

## 3. Rules

- The tenant comes from trusted context: the application's configured
  repository locations and the installation the token reaches. A URL,
  query or body naming a tenant is a request to be validated against
  memberships, never a source of truth (FID-DATA-003, FID-ORG-007).
- Switching organization re-derives the context and drops cached views
  (FID-ORG-003).
- Identity and membership are separate: removing a membership leaves the
  identity; losing a GitHub account leaves no membership that grants
  access, because access is GitHub's (FID-ORG-002).
- Invitations are single-use, expiring and bound to the accepting subject
  (FID-ORG-006). Delegation is deferred (FID-ORG-005).

## 4. Required tests

| Test | Level | Requirement |
|---|---|---|
| Tenant A's token reading tenant B's repository, through Arca and directly | live-provider | FID-TEST-006 |
| URL naming another tenant yields "not permitted" with no request to it | unit, simulated-provider | FID-DATA-003, FID-ORG-007 |
| Member writes the administrators-only role record directly | live-provider | FID-TEST-007, FID-AUZ-006 |
| Administrator of A grants in B | unit, live-provider | FID-AUZ-007, FID-ORG-004 |
| Application A's refresh token refreshed through B's registration | unit, simulated-provider, live-provider | FID-AUZ-009 |

## 5. Open questions for the owner

1. Is the tenant the GitHub App installation (recommended), the GitHub
   organization, or an Arca namespace inside shared repositories? The last
   is not an enforced boundary.
2. Should each production application get its own GitHub App, or may some
   applications share an isolation domain?
3. Which application needs more than one organization first, and when?
