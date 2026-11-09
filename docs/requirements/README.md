---
id: FIDES-REQUIREMENTS-INDEX
title: Fides requirements index
status: draft
version: 0.3.0
created: 2026-11-09
updated: 2026-11-09
owners:
  - fides
tags: [requirements, index]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:50.705Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
---

# Fides requirements index

| Document | What it is |
|---|---|
| [`FIDES-REQUIREMENTS.md`](FIDES-REQUIREMENTS.md) | **Canonical catalogue.** 130 requirements, each with priority, owner, status, acceptance criteria, positive and negative tests, evidence and verification levels |
| [`TRACEABILITY-MATRIX.md`](TRACEABILITY-MATRIX.md) | Requirement → implementation → test, with counts; generated from the catalogue |
| [`VERIFICATION-PLAN.md`](VERIFICATION-PLAN.md) | Verification levels, CI gates, Dokimos integration, the mandatory adversarial tests |
| [`ORGANIZATION-AND-TENANT-ISOLATION.md`](ORGANIZATION-AND-TENANT-ISOLATION.md) | The tenancy model the FID-ORG and FID-DATA requirements assume |
| [`UI-REQUIREMENTS-AND-SCREENS.md`](UI-REQUIREMENTS-AND-SCREENS.md) | Forma screen inventory and failure-class mapping for FID-UX |
| [`backlog-plan.md`](backlog-plan.md) | Prioritized, dependency-ordered backlog |
| [`READINESS-AND-GAP-REPORT.md`](READINESS-AND-GAP-REPORT.md) | Readiness, gaps, violations, decisions awaiting the owner |
| [`../architecture/SECURITY-ARCHITECTURE.md`](../architecture/SECURITY-ARCHITECTURE.md) | Components, boundaries, authority for each permission check |
| [`../architecture/AUTHORIZATION-OWNERSHIP.md`](../architecture/AUTHORIZATION-OWNERSHIP.md) | Authorization ownership proposal |
| [`../security/THREAT-MODEL.md`](../security/THREAT-MODEL.md) | Tutela-style threat model |

## Families

| Prefix | Family | Mission area |
|---|---|---|
| FID-CTX | Repository context | — |
| FID-TB | Trust boundaries | — |
| FID-PRV | Provider model | A |
| FID-EXC | Code-for-token exchange | A |
| FID-HOST | Hosting | F |
| FID-CLI | Client library | E |
| FID-TEST | Verification obligations | Phase 4 |
| FID-IDN | Identity and authentication | A |
| FID-SES | Sessions | A |
| FID-SVC | Non-human identities | A |
| FID-AUZ | Authorization | B |
| FID-DATA | Data-layer security | C |
| FID-ORG | Multiple organizations | D |
| FID-UX | Application integration and UX | E |
| FID-OPS | Operational security | F |
| FID-REL | Reliability and performance | G |

## Changing a requirement

1. Begin a work item (`./praxis work begin`).
2. Edit the record in `FIDES-REQUIREMENTS.md`. Keep its ID; never reuse an
   ID. Adding an unmet obligation to a verified requirement is a new
   requirement, not a revision.
3. A status may move to `implemented-verified` only when a test carries
   `[<Trait("Verifies", "FID-…")>]` and the achieved levels cover the
   required ones.
4. Regenerate the matrix:
   `FIDES_REGENERATE_MATRIX=1 dotnet test tests/Fides.Tests -c Release --filter "FullyQualifiedName~RequirementsCatalogueTests"`.
5. Run `dotnet test Fides.slnx -c Release` and `./praxis validate`.
