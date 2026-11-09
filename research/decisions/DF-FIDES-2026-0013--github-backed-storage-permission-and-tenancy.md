---
id: DF-FIDES-2026-0013
title: With browser-held user tokens, GitHub repository permission is the only enforced data boundary; one GitHub App per data-isolation domain
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
tags: [github, tenancy, permissions, security]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:48.204Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
---

# DF-FIDES-2026-0013 — GitHub-backed storage: permission and tenancy conflicts

- **Date:** 2026-11-09
- **Status:** proposed (`review`); **needs owner approval**.
- **Answers:** WI-0022 phase 3 question 4. Requirements: FID-AUZ-004,
  FID-AUZ-009, FID-DATA-002, FID-DATA-004, FID-DATA-006.

## Findings (conflicts exist)

1. **Application permissions are bypassable.** The browser holds a
   user-to-server token whose reach is the intersection of the App's
   permissions, the installation and the user's own access. Any user with
   repository write can write through the GitHub API directly, whatever the
   application's own role model says (SEC-THR-109). "Server-side
   enforcement for all protected operations" cannot be met for data held in
   repositories the user can write.
2. **Tenancy follows repositories, not folders.** GitHub enforces
   permissions per repository; Arca already requires one repository per
   permission boundary (ARCA-LOC-004).
3. **Applications share token reach.** The hosting guide registers one App
   per environment for all applications (ten callback URLs per App), and the
   exchange keys clients by provider. A Chrona token can reach Summa's
   repositories when the same person and installation cover both, and can be
   refreshed through Summa's registration (SEC-THR-107).
4. **Arca stores no encryption at rest** (ARCA-LOC-010): GitHub and anyone
   with repository read see the data.

## Options

| Option | For | Against |
|---|---|---|
| A. Repository permission is the enforced boundary (recommended) | Honest; matches Arca; no new component | Applications must design data layouts around repositories; finer permissions are advisory |
| B. Server-side enforcement point with an installation token | Finer, enforceable permissions | Needs DF-FIDES-2026-0010 option C and DF-FIDES-2026-0014; operational cost |
| C. Keep one shared App and rely on application checks | Simple | Accepts finding 1 and 3 silently; rejected |

## Recommendation

Adopt **A**, with **one GitHub App per data-isolation domain** (at least per
application in production) and exchange support for a provider client per
application (FID-DATA-006, FID-AUZ-009). Classify every application
permission as repository-enforced, server-enforced or advisory
(FID-AUZ-004). Adopt **B** only through an accepted decision when a consumer
needs a permission finer than a repository.
