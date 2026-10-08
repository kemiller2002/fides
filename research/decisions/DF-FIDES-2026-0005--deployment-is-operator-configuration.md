---
id: DF-FIDES-2026-0005
title: The AWS account, region and domain are operator configuration; nothing is deployed from this work and no secret enters the repository
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
  - research/decisions/DF-FIDES-2026-0001--cloud-agnostic-exchange-aws-first.md
tags: [hosting, aws, deployment, secrets, configuration]
provenance:
  contributions:
    EXE-20261008T084302418Z-58dc4016:
      operations: [created]
      at: 2026-10-08T08:46:19.210Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "User decision of 2026-10-08 recorded for WI-0003"
---

# DF-FIDES-2026-0005 — Deployment targets are configuration

- **Date:** 2026-10-08
- **Status:** accepted (user decision of 2026-10-08; resolves OQ-FIDES-003
  by making it configuration)

## Context

AWS Lambda behind an HTTP API is the first host (DF-FIDES-2026-0001). The
AWS account, region and domain are not chosen yet, and the user asked that
nothing be deployed.

## Decision

1. The account, region, domain, environment name, GitHub App identifiers
   and secret locations are **parameters** of the infrastructure template and
   of the exchange's runtime configuration. None is hard-coded.
2. **Nothing is deployed** by this work: the adapter ships as code, tests and
   a template that can be validated and emulated locally.
3. **No secret enters the repository.** The GitHub App client secret lives
   in the operator's secret store (AWS Secrets Manager first) and is read at
   runtime by reference. Tests use synthetic values that are visibly fake.
4. The hosting documentation lists exactly what the operator must provide.

## Consequences

- The same template deploys test, staging and production with different
  parameters and different GitHub App registrations (FID-HOST-003).
- Deployment waits on the operator's choices, but building and testing do
  not.
