---
id: DF-FIDES-2026-0001
title: Fides's code-for-token exchange is a pure, cloud-agnostic core behind a thin hosting adapter; AWS (Lambda behind API Gateway) is the first target and Azure stays possible
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
tags: [hosting, aws, azure, architecture]
provenance:
  contributions:
    EXE-20261008T082246199Z-61dcdd53:
      operations: [created]
      at: 2026-10-08T08:23:19.834Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Consolidated from issue #1, user decisions of 2026-10-08 and consumer sign-in requirements"
---

# DF-FIDES-2026-0001 — Cloud-agnostic exchange, AWS first

- **Date:** 2026-10-08
- **Status:** accepted (user decision of 2026-10-08)

## Context

OAuth's authorization-code flow needs a server-side component to exchange the
code for a token, because that step uses the client secret, which must never
reach the browser. The user expects to host on AWS and may host on Azure.

## Decision

- The exchange is a **pure F# core**: request, configuration and provider
  responses go in as data; a response or a typed refusal comes out; effects
  are explicit (FID-EXC-002).
- A **thin hosting adapter** translates the host's request and response
  shapes. The core references no cloud SDK (FID-HOST-001).
- **AWS first:** Lambda behind API Gateway (HTTP API), with the client secret
  in Secrets Manager or SSM Parameter Store (FID-HOST-002).
- **Azure stays possible:** an Azure Functions plus Key Vault adapter can be
  added without touching the core, proved by a shared adapter conformance
  suite (FID-HOST-004). Building it is deferred.

## Consequences

- Moving clouds means adding an adapter, not rewriting the core.
- Adapter behaviour stays honest because every adapter runs the same
  conformance suite.
