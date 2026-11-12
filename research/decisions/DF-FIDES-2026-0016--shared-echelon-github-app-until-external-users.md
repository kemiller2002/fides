---
id: DF-FIDES-2026-0016
title: One shared Echelon GitHub App per environment while the owner is the only user; split into one App per application before anyone else signs in; repository permission stays the only enforced data boundary
status: accepted
version: 1.0.0
created: 2026-11-11
updated: 2026-11-11
owners:
  - fides
review_cycle: on-trigger
supersedes: [DF-FIDES-2026-0013]
superseded_by: []
related_documents:
  - research/decisions/DF-FIDES-2026-0013--github-backed-storage-permission-and-tenancy.md
  - docs/requirements/FIDES-REQUIREMENTS.md
  - docs/hosting/AWS.md
tags: [github, tenancy, permissions, security, sso]
provenance:
  contributions:
    EXE-20261111T150817979Z-d906ff87:
      operations: [created]
      at: 2026-11-11T15:13:10.511Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Shared Echelon GitHub App and command-line App creation (WI-0042, owner decision of 2026-11-11)"
---

# DF-FIDES-2026-0016 — One shared Echelon GitHub App until external users

- **Date:** 2026-11-11
- **Status:** accepted (owner decision of 2026-11-11). Supersedes
  DF-FIDES-2026-0013.

## Context

DF-FIDES-2026-0013 found that, with browser-held user tokens, the GitHub
repository permission is the only enforced data boundary, and it chose one
GitHub App per data-isolation domain, at least one per application in
production. While deploying the first production environment, the owner
asked for one sign-on across every Echelon application and confirmed:

- the owner is the only user;
- a user-to-server token reaches a repository only when the App is
  installed on it, the App's permissions allow the action, **and the person
  can access the repository**, so a person without access to an
  application's data repository cannot reach that data through any shared
  App;
- the application is expected to be commercialized later.

Either choice gives single sign-on: one GitHub login signs the person in to
every application. The difference is one "Authorize" click per App, and
how far one application's token reaches.

## Decision

1. **Kept from DF-FIDES-2026-0013:** GitHub repository permission is the
   only enforced data boundary for Arca-stored data. Application permissions
   finer than a repository are advisory unless a server-side enforcement
   point is approved. Each permission boundary is its own repository
   (ARCA-LOC-004). (FID-AUZ-004, FID-DATA-002, FID-DATA-004)
2. **Changed:** each environment uses **one shared GitHub App** (for
   production, `Echelon (production)`) for every Echelon application while
   the owner is the only person who signs in. All applications in that
   environment are one data-isolation domain.
3. **Condition:** before any person other than the owner signs in to any
   Echelon application, each application MUST get its own GitHub App
   (FID-DATA-009), which needs exchange support for a provider client per
   application (FID-DATA-006, WI-0025). Moving does not move data: each new
   App is installed on that application's repositories and each person
   authorizes it once.
4. A shared App is limited to ten callback URLs, so at most ten
   applications per environment.

## Accepted risks while the condition holds

- A sign-in token from any application can reach every data repository the
  person can access, so a defect or injected script in one application can
  read or write another application's data (SEC-THR-107).
- The exchange's refresh and revoke accept any registered application's
  token (FID-AUZ-009); within one isolation domain this grants no extra
  reach.

## Consequences

- The deployment script registers several applications against one App and
  can create the App from the command line (WI-0042).
- FID-DATA-009 tracks the split; it must be verified before external use.
