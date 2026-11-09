---
id: FIDES-TRACEABILITY-MATRIX
title: Fides requirement traceability matrix
status: draft
version: 0.3.0
created: 2026-11-09
updated: 2026-11-09
owners:
  - fides
related_documents:
  - docs/requirements/FIDES-REQUIREMENTS.md
  - docs/requirements/VERIFICATION-PLAN.md
tags: [requirements, traceability, verification]
provenance:
  contributions:
    EXE-20261109T142541284Z-a3f0d18d:
      operations: [created]
      at: 2026-11-09T14:44:51.093Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Fides requirements expansion and security architecture review (WI-0022)"
---

# Fides requirement traceability matrix

Requirement to implementation to test, generated from
[`FIDES-REQUIREMENTS.md`](FIDES-REQUIREMENTS.md) by
`tests/Fides.Tests/RequirementsCatalogue.fs` (FID-TEST-005). The test suite
fails when this section differs from the catalogue. To regenerate after
editing the catalogue:

```bash
FIDES_REGENERATE_MATRIX=1 dotnet test tests/Fides.Tests -c Release --filter "FullyQualifiedName~RequirementsCatalogueTests"
```

Status meanings: `implemented-verified` means a test declares the
requirement (`[<Trait("Verifies", ID)>]`) and the achieved levels include
every required level; `implemented-unverified` means behaviour or a
document exists but the required evidence does not; `missing`, `blocked`
and `deferred` mean no implementation. A `live-provider` level is never
satisfied by the GitHub simulator.

<!-- matrix:start -->
<!-- Generated from docs/requirements/FIDES-REQUIREMENTS.md by tests/Fides.Tests/RequirementsCatalogue.fs. Do not edit by hand: run the tests with FIDES_REGENERATE_MATRIX=1. -->

## Summary by family and status

| Family | implemented-verified | implemented-unverified | missing | blocked | deferred | Total |
|---|---:|---:|---:|---:|---:|---:|
| Repository context | 3 | 0 | 0 | 0 | 0 | 3 |
| Trust boundaries | 6 | 0 | 0 | 0 | 0 | 6 |
| Provider model | 4 | 0 | 0 | 0 | 0 | 4 |
| Code-for-token exchange | 5 | 0 | 0 | 0 | 0 | 5 |
| Hosting | 5 | 0 | 0 | 0 | 0 | 5 |
| Client library | 4 | 0 | 0 | 0 | 0 | 4 |
| Verification obligations | 5 | 1 | 2 | 6 | 0 | 14 |
| A. Identity and authentication | 3 | 2 | 3 | 0 | 2 | 10 |
| A. Sessions | 1 | 2 | 4 | 3 | 0 | 10 |
| A. Non-human identities | 0 | 0 | 0 | 4 | 0 | 4 |
| B. Authorization | 0 | 2 | 3 | 6 | 0 | 11 |
| C. Data-layer security | 1 | 2 | 2 | 2 | 1 | 8 |
| D. Multi-organization | 0 | 0 | 0 | 6 | 1 | 7 |
| E. Application integration and UX | 0 | 1 | 12 | 1 | 1 | 15 |
| F. Operational security | 3 | 3 | 6 | 2 | 0 | 14 |
| G. Reliability and performance | 4 | 3 | 1 | 2 | 0 | 10 |
| **Total** | 44 | 16 | 33 | 32 | 5 | 130 |

## Summary by family and priority

| Family | P0 | P1 | P2 | P3 | Total |
|---|---:|---:|---:|---:|---:|
| Repository context | 0 | 3 | 0 | 0 | 3 |
| Trust boundaries | 6 | 0 | 0 | 0 | 6 |
| Provider model | 2 | 1 | 1 | 0 | 4 |
| Code-for-token exchange | 4 | 1 | 0 | 0 | 5 |
| Hosting | 1 | 3 | 0 | 1 | 5 |
| Client library | 3 | 1 | 0 | 0 | 4 |
| Verification obligations | 9 | 5 | 0 | 0 | 14 |
| A. Identity and authentication | 4 | 2 | 2 | 2 | 10 |
| A. Sessions | 4 | 4 | 2 | 0 | 10 |
| A. Non-human identities | 0 | 3 | 1 | 0 | 4 |
| B. Authorization | 6 | 5 | 0 | 0 | 11 |
| C. Data-layer security | 6 | 0 | 2 | 0 | 8 |
| D. Multi-organization | 1 | 3 | 2 | 1 | 7 |
| E. Application integration and UX | 1 | 10 | 3 | 1 | 15 |
| F. Operational security | 3 | 10 | 1 | 0 | 14 |
| G. Reliability and performance | 4 | 4 | 2 | 0 | 10 |
| **Total** | 54 | 55 | 16 | 5 | 130 |

## Summary by change

| Family | retained | revised | added | Total |
|---|---:|---:|---:|---:|
| Repository context | 3 | 0 | 0 | 3 |
| Trust boundaries | 5 | 1 | 0 | 6 |
| Provider model | 4 | 0 | 0 | 4 |
| Code-for-token exchange | 4 | 1 | 0 | 5 |
| Hosting | 4 | 1 | 0 | 5 |
| Client library | 3 | 1 | 0 | 4 |
| Verification obligations | 2 | 0 | 12 | 14 |
| A. Identity and authentication | 0 | 0 | 10 | 10 |
| A. Sessions | 0 | 0 | 10 | 10 |
| A. Non-human identities | 0 | 0 | 4 | 4 |
| B. Authorization | 0 | 0 | 11 | 11 |
| C. Data-layer security | 0 | 0 | 8 | 8 |
| D. Multi-organization | 0 | 0 | 7 | 7 |
| E. Application integration and UX | 0 | 0 | 15 | 15 |
| F. Operational security | 0 | 0 | 14 | 14 |
| G. Reliability and performance | 0 | 0 | 10 | 10 |
| **Total** | 25 | 4 | 101 | 130 |

## Requirement to implementation to test

| Requirement | Priority | Owner | Status | Change | Evidence (implementation and tests) | Levels achieved | Levels required |
|---|---|---|---|---|---|---|---|
| FID-CTX-001 | P1 | Fides.Governance | implemented-verified | retained | PROJECT-CHARTER.md; tests/Fides.Tests/RepositoryContextTests.fs | unit | unit |
| FID-CTX-002 | P1 | Fides.Governance | implemented-verified | retained | tests/Fides.Tests/RepositoryContext.fs; tests/Fides.Tests/RepositoryContextTests.fs | unit | unit |
| FID-CTX-003 | P1 | Fides.Governance | implemented-verified | retained | tests/Fides.Tests/RepositoryContextTests.fs | unit | unit |
| FID-TB-001 | P0 | Fides.Governance | implemented-verified | retained | docs/architecture/TRUST-BOUNDARIES.md; docs/architecture/SECURITY-ARCHITECTURE.md; tests/Fides.Tests/TrustBoundaryModelTests.fs | document, unit | document, unit |
| FID-TB-002 | P0 | Fides.Governance | implemented-verified | retained | docs/architecture/TRUST-BOUNDARIES.md section 3; tests/Fides.Tests/SecretsTests.fs | document, unit | document, unit |
| FID-TB-003 | P0 | Fides.Core | implemented-verified | retained | src/Fides/Exchange.fs; src/Fides/Configuration.fs; tests/Fides.Tests/ExchangeSecurityTests.fs; tests/Fides.Tests/ConfigurationTests.fs | document, unit, simulated-provider | unit, simulated-provider |
| FID-TB-004 | P0 | Fides.Governance | implemented-verified | revised | docs/architecture/TRUST-BOUNDARIES.md section 5; docs/architecture/AUTHORIZATION-OWNERSHIP.md | document, unit, simulated-provider | document, unit |
| FID-TB-005 | P0 | Fides.Client | implemented-verified | retained | src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs; tests/Fides.Tests/Acceptance/ClientAcceptanceTests.fs | document, unit, simulated-provider | unit, simulated-provider |
| FID-TB-006 | P0 | Fides.Governance | implemented-verified | retained | research/decisions/DF-FIDES-2026-0007--what-github-proves-and-what-fides-owns.md | document, unit | document |
| FID-PRV-001 | P1 | Fides.Core | implemented-verified | retained | src/Fides/Provider.fs; tests/Fides.Tests/ProviderTests.fs | unit | unit |
| FID-PRV-002 | P0 | Fides.Core | implemented-verified | retained | src/Fides/GitHub.fs; tests/Fides.Tests/ProviderTests.fs | unit, simulated-provider | unit, simulated-provider |
| FID-PRV-003 | P0 | Fides.Core | implemented-verified | retained | src/Fides/GitHub.fs; tests/Fides.Tests/ProviderTests.fs; tests/Fides.Tests/ClientEndToEndTests.fs | unit, simulated-provider, browser-wasm | unit, simulated-provider |
| FID-PRV-004 | P2 | Fides.Core | implemented-verified | retained | src/Fides/Provider.fs; tests/Fides.Tests/ProviderCatalogTests.fs | unit | unit |
| FID-EXC-001 | P0 | Fides.Core | implemented-verified | retained | src/Fides/Exchange.fs; src/Fides/Pkce.fs; src/Fides.Client/SignIn.fs; tests/Fides.Tests/PkceTests.fs; tests/Fides.Tests/ExchangeSecurityT... | unit, simulated-provider, browser-wasm | unit, simulated-provider |
| FID-EXC-002 | P1 | Fides.Core | implemented-verified | retained | src/Fides/Effect.fs; tests/Fides.Tests/CoreBoundaryTests.fs | unit | unit |
| FID-EXC-003 | P0 | Fides.Core | implemented-verified | revised | src/Fides.Client/FidesClient.fs; tests/Fides.Tests/Acceptance/ClientAcceptanceTests.fs; tests/Fides.Tests/Acceptance/ExchangeAcceptanceTe... | unit, simulated-provider | unit, simulated-provider |
| FID-EXC-004 | P0 | Fides.Core | implemented-verified | retained | src/Fides/Exchange.fs; tests/Fides.Tests/Acceptance/ExchangeAcceptanceTests.fs; tests/Fides.Hosting.Aws.Tests/FunctionTests.fs | unit, simulated-provider, emulated-host | unit, simulated-provider |
| FID-EXC-005 | P0 | Fides.Hosting | implemented-verified | retained | src/Fides/Secrets.fs; src/Fides.Hosting/Diagnostics.fs; tests/Fides.Hosting.Aws.Tests/DiagnosticsTests.fs; tests/Fides.Tests/SecretsTests.fs | unit, simulated-provider, emulated-host | unit, emulated-host |
| FID-HOST-001 | P1 | Fides.Hosting | implemented-verified | retained | src/Fides/Service.fs; src/Fides.Hosting.Aws/Translation.fs; tests/Fides.Hosting.Aws.Tests/TranslationTests.fs | unit | unit |
| FID-HOST-002 | P1 | Fides.Hosting.Aws | implemented-verified | retained | src/Fides.Hosting.Aws; tests/Fides.Hosting.Aws.Tests/RuntimeEmulationTests.fs; tests/Fides.Hosting.Aws.Tests/SecretsTests.fs | unit, emulated-host | unit, emulated-host |
| FID-HOST-003 | P1 | Fides.Hosting.Aws | implemented-verified | revised | infrastructure/aws/fides-exchange.template.json; tests/Fides.Hosting.Aws.Tests/TemplateTests.fs | unit | unit |
| FID-HOST-004 | P3 | Fides.Hosting | implemented-verified | retained | tests/Fides.Acceptance/Conformance.fs; tests/Fides.Hosting.Aws.Tests/ConformanceTests.fs | unit, emulated-host | unit |
| FID-HOST-005 | P0 | Fides.Core | implemented-verified | retained | src/Fides/Service.fs; tests/Fides.Tests/ExchangeSecurityTests.fs; tests/Fides.Hosting.Aws.Tests/FunctionTests.fs | unit, simulated-provider, emulated-host | unit, emulated-host |
| FID-CLI-001 | P0 | Fides.Client | implemented-verified | retained | src/Fides.Client; src/Fides.Arca/TokenBridge.fs; tests/Fides.Client.Wasm; tests/Fides.Tests/ArcaBridgeTests.fs | unit, simulated-provider, browser-wasm | unit, browser-wasm |
| FID-CLI-002 | P0 | Fides.Client | implemented-verified | revised | src/Fides.Client/Model.fs; src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs | unit | unit |
| FID-CLI-003 | P0 | Fides.Client | implemented-verified | retained | src/Fides.Client/Model.fs; tests/Fides.Tests/ClientTests.fs; tests/Fides.Tests/Acceptance/ClientAcceptanceTests.fs | unit, simulated-provider | unit |
| FID-CLI-004 | P1 | Fides.Release | implemented-verified | retained | .github/workflows/release.yml; release/echelon.release-input.json; tests/Fides.Tests/ReleaseTests.fs | unit | unit |
| FID-TEST-001 | P0 | Fides.Governance | implemented-verified | retained | tests/Fides.Acceptance/Scenarios.fs; tests/Fides.Tests/Acceptance | unit, simulated-provider | simulated-provider |
| FID-TEST-002 | P1 | Fides.Hosting | implemented-verified | retained | tests/Fides.Acceptance/Conformance.fs | unit, emulated-host | emulated-host |
| FID-TEST-003 | P0 | Fides.Governance | implemented-verified | added | tests/Fides.Tests/RequirementsCatalogue.fs; tests/Fides.Tests/RequirementsCatalogueTests.fs | unit | unit |
| FID-TEST-004 | P0 | Fides.Governance | implemented-verified | added | tests/Fides.Tests/RequirementsCatalogueTests.fs | unit | unit |
| FID-TEST-005 | P1 | Fides.Governance | implemented-verified | added | docs/requirements/TRACEABILITY-MATRIX.md | unit | unit |
| FID-TEST-006 | P0 | Fides.Governance | blocked | added | none | none | simulated-provider, live-provider |
| FID-TEST-007 | P0 | Fides.Governance | blocked | added | none | none | unit, simulated-provider, live-provider |
| FID-TEST-008 | P1 | Fides.Governance | missing | added | none | none | unit, simulated-provider |
| FID-TEST-009 | P0 | Fides.Governance | missing | added | none | none | unit, simulated-provider |
| FID-TEST-010 | P0 | Fides.Governance | blocked | added | none | none | live-provider |
| FID-TEST-011 | P0 | Fides.Governance | blocked | added | none | none | live-provider |
| FID-TEST-012 | P0 | Fides.Governance | blocked | added | none | none | live-provider |
| FID-TEST-013 | P1 | Fides.Governance | blocked | added | none | none | live-provider |
| FID-TEST-014 | P1 | Fides.Governance | implemented-unverified | added | .github/workflows/dokimos.yml; .dokimos/policy.json; docs/requirements/VERIFICATION-PLAN.md | document | integration |
| FID-IDN-001 | P0 | Fides.Core | implemented-verified | added | src/Fides/GitHub.fs; src/Fides/Protocol.fs; tests/Fides.Tests/ProviderTests.fs | unit, simulated-provider | unit, simulated-provider |
| FID-IDN-002 | P0 | Fides.Core | implemented-verified | added | src/Fides/Exchange.fs; src/Fides/GitHub.fs; tests/Fides.Tests/ProviderTests.fs | unit, simulated-provider | unit, simulated-provider |
| FID-IDN-003 | P1 | Fides.Client | missing | added | none | none | unit |
| FID-IDN-004 | P0 | Fides.Client | implemented-unverified | added | src/Fides.Client/FidesClient.fs (pendingKey) | none | unit |
| FID-IDN-005 | P0 | Fides.Client | implemented-verified | added | src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs | unit | unit |
| FID-IDN-006 | P3 | Fides.Core | deferred | added | none | none | unit, simulated-provider |
| FID-IDN-007 | P3 | Fides.Core | deferred | added | none | none | unit |
| FID-IDN-008 | P2 | Fides.Governance | missing | added | none | none | document |
| FID-IDN-009 | P1 | Fides.Client | implemented-unverified | added | src/Fides.Client/FidesClient.fs (completeCallback) | none | unit, simulated-provider |
| FID-IDN-010 | P2 | Fides.Client | missing | added | none | none | unit |
| FID-SES-001 | P1 | Fides.Client | missing | added | none | none | unit |
| FID-SES-002 | P2 | Fides.Client | missing | added | none | none | unit |
| FID-SES-003 | P1 | Fides.Client | missing | added | none | none | unit, browser-wasm |
| FID-SES-004 | P0 | Fides.Core | blocked | added | none | none | simulated-provider, live-provider |
| FID-SES-005 | P0 | Fides.Client | missing | added | none | none | unit, simulated-provider |
| FID-SES-006 | P0 | Fides.Client | implemented-unverified | added | src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs | unit | unit, live-provider |
| FID-SES-007 | P1 | Fides.Client | implemented-verified | added | src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs | unit | unit |
| FID-SES-008 | P0 | Owner decision | blocked | added | none | none | unit, browser-wasm |
| FID-SES-009 | P1 | Fides.Client | implemented-unverified | added | src/Fides.Client/FidesClient.fs (restore) | none | unit |
| FID-SES-010 | P2 | Fides.Core | blocked | added | none | none | simulated-provider, live-provider |
| FID-SVC-001 | P1 | Owner decision | blocked | added | none | none | unit |
| FID-SVC-002 | P1 | Owner decision | blocked | added | none | none | unit, live-provider |
| FID-SVC-003 | P1 | Owner decision | blocked | added | none | none | unit |
| FID-SVC-004 | P2 | Owner decision | blocked | added | none | none | unit |
| FID-AUZ-001 | P0 | Fides.Governance | implemented-unverified | added | docs/architecture/AUTHORIZATION-OWNERSHIP.md | document | document, unit |
| FID-AUZ-002 | P0 | Application | missing | added | none | none | simulated-provider, live-provider |
| FID-AUZ-003 | P0 | Owner decision | blocked | added | none | none | unit |
| FID-AUZ-004 | P0 | Owner decision | blocked | added | none | none | live-provider |
| FID-AUZ-005 | P1 | Application | blocked | added | none | none | unit |
| FID-AUZ-006 | P1 | Owner decision | blocked | added | none | none | live-provider |
| FID-AUZ-007 | P0 | Owner decision | blocked | added | none | none | unit, live-provider |
| FID-AUZ-008 | P1 | Fides.Core | missing | added | none | none | unit, simulated-provider |
| FID-AUZ-009 | P0 | Fides.Core | missing | added | none | none | unit, simulated-provider, live-provider |
| FID-AUZ-010 | P1 | Operator | implemented-unverified | added | docs/hosting/AWS.md | document | unit, live-provider |
| FID-AUZ-011 | P1 | Owner decision | blocked | added | none | none | live-provider |
| FID-DATA-001 | P0 | Fides.Arca | implemented-verified | added | src/Fides.Arca/TokenBridge.fs; tests/Fides.Tests/ArcaBridgeTests.fs | unit | unit |
| FID-DATA-002 | P0 | Arca | implemented-unverified | added | docs/architecture/SECURITY-ARCHITECTURE.md | document | document, live-provider |
| FID-DATA-003 | P0 | Owner decision | blocked | added | none | none | unit, simulated-provider |
| FID-DATA-004 | P0 | Arca | blocked | added | none | none | live-provider |
| FID-DATA-005 | P2 | Strata | deferred | added | none | none | integration |
| FID-DATA-006 | P0 | Fides.Core | missing | added | none | none | unit, simulated-provider |
| FID-DATA-007 | P2 | Arca | missing | added | none | none | unit |
| FID-DATA-008 | P0 | Fides.Core | implemented-unverified | added | src/Fides/Exchange.fs; src/Fides/GitHub.fs (repositoryAccessRequest) | none | unit |
| FID-ORG-001 | P1 | Owner decision | blocked | added | none | none | unit |
| FID-ORG-002 | P1 | Owner decision | blocked | added | none | none | unit |
| FID-ORG-003 | P1 | Application | blocked | added | none | none | unit, browser-wasm |
| FID-ORG-004 | P2 | Owner decision | blocked | added | none | none | unit, live-provider |
| FID-ORG-005 | P3 | Owner decision | deferred | added | none | none | unit |
| FID-ORG-006 | P2 | Owner decision | blocked | added | none | none | unit, live-provider |
| FID-ORG-007 | P0 | Owner decision | blocked | added | none | none | unit, simulated-provider |
| FID-UX-001 | P1 | Fides.Client | implemented-unverified | added | src/Fides.Client/FidesClient.fs (ClientPorts); scripts/verify-wasm.sh | browser-wasm | unit, browser-wasm |
| FID-UX-002 | P1 | Fides.Client | missing | added | none | none | browser-wasm |
| FID-UX-003 | P1 | Fides.UI | missing | added | none | none | browser-wasm |
| FID-UX-004 | P2 | Fides.UI | missing | added | none | none | browser-wasm, manual-accessibility |
| FID-UX-005 | P1 | Fides.UI | missing | added | none | none | browser-wasm |
| FID-UX-006 | P1 | Fides.UI | missing | added | none | none | browser-wasm |
| FID-UX-007 | P1 | Fides.UI | missing | added | none | none | browser-wasm |
| FID-UX-008 | P1 | Fides.UI | missing | added | none | none | browser-wasm, manual-accessibility |
| FID-UX-009 | P1 | Fides.UI | missing | added | none | none | browser-wasm, manual-accessibility |
| FID-UX-010 | P1 | Fides.UI | missing | added | none | none | unit, browser-wasm |
| FID-UX-011 | P3 | Owner decision | deferred | added | none | none | document |
| FID-UX-012 | P2 | Fides.UI | blocked | added | none | none | browser-wasm |
| FID-UX-013 | P1 | Application | missing | added | none | none | browser-wasm |
| FID-UX-014 | P0 | Application | missing | added | none | none | browser-wasm |
| FID-UX-015 | P2 | Fides.UI | missing | added | none | none | browser-wasm |
| FID-OPS-001 | P0 | Fides.Hosting | implemented-verified | added | src/Fides.Hosting/SecretCache.fs; src/Fides/Configuration.fs; tests/Fides.Tests/ConfigurationTests.fs | unit | unit |
| FID-OPS-002 | P0 | Fides.Hosting.Aws | implemented-unverified | added | infrastructure/aws/fides-exchange.template.json (AccessLogSettings) | document | unit |
| FID-OPS-003 | P1 | Fides.Hosting.Aws | missing | added | none | none | unit |
| FID-OPS-004 | P1 | Fides.Hosting | missing | added | none | none | unit, emulated-host |
| FID-OPS-005 | P1 | Fides.Hosting.Aws | implemented-verified | added | infrastructure/aws/fides-exchange.template.json; tests/Fides.Hosting.Aws.Tests/TemplateTests.fs | unit | unit |
| FID-OPS-006 | P2 | Fides.Hosting.Aws | missing | added | none | none | unit |
| FID-OPS-007 | P1 | Fides.Hosting | missing | added | none | none | unit, emulated-host |
| FID-OPS-008 | P0 | Fides.Core | implemented-verified | added | src/Fides/Configuration.fs; src/Fides.Hosting.Aws/Program.fs; tests/Fides.Tests/ConfigurationTests.fs | unit | unit |
| FID-OPS-009 | P1 | Fides.Release | missing | added | none | none | integration |
| FID-OPS-010 | P1 | Fides.Release | implemented-unverified | added | .conditor/lock.json; docs/client/README.md | document | integration |
| FID-OPS-011 | P1 | Fides.Governance | implemented-unverified | added | docs/security/THREAT-MODEL.md | document | document, unit |
| FID-OPS-012 | P1 | Operator | missing | added | none | none | document, live-provider |
| FID-OPS-013 | P1 | Operator | blocked | added | none | none | live-provider |
| FID-OPS-014 | P1 | Operator | blocked | added | docs/hosting/AWS.md | none | live-provider |
| FID-REL-001 | P2 | Owner decision | blocked | added | none | none | live-provider |
| FID-REL-002 | P2 | Owner decision | blocked | added | none | none | unit, live-provider |
| FID-REL-003 | P0 | Fides.Core | implemented-verified | added | src/Fides/GitHub.fs; tests/Fides.Tests/ProviderTests.fs | unit | unit |
| FID-REL-004 | P0 | Fides.Client | implemented-verified | added | src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs | unit | unit |
| FID-REL-005 | P1 | Fides.Client | implemented-verified | added | src/Fides.Client/FidesClient.fs; tests/Fides.Tests/ClientTests.fs | unit | unit |
| FID-REL-006 | P0 | Fides.Client | missing | added | none | none | unit, simulated-provider |
| FID-REL-007 | P1 | Fides.Client | implemented-unverified | added | src/Fides.Client/Tokens.fs | none | unit |
| FID-REL-008 | P1 | Fides.Client | implemented-verified | added | src/Fides.Client/SignIn.fs; tests/Fides.Tests/ClientTests.fs | unit | unit |
| FID-REL-009 | P1 | Fides.Hosting | implemented-unverified | added | src/Fides.Hosting.Aws/Function.fs; infrastructure/aws/fides-exchange.template.json | none | unit, emulated-host |
| FID-REL-010 | P0 | Fides.Client | implemented-unverified | added | src/Fides.Client/FidesClient.fs; tests/Fides.Acceptance/Scenarios.fs (`expired-session-outage`) | unit, simulated-provider | unit, simulated-provider |
<!-- matrix:end -->
