# Work Queue

| ID | Work | Status | Tags | Priority |
|---|---|---|---|---|
| ROS-INSTALL-3-7-2 | ROS-INSTALL-3-7-2 | complete |  |  |
| WI-0001 | Complete Fides's Conditor setup: echelon-current 1.3.0 (Ordo 1.5.0) through conditor upgrade --current, plus the fsharp-nuget-library scaffold (buildable project, foundations, build-and-test, release workflow); supersedes fides#2 | complete | conditor, setup | high |
| WI-0002 | Consolidate Fides's SSO requirements, record the hosting and provider decisions, and capture the dependency-ordered backlog | complete | planning, requirements | high |
| WI-0003 | Fides slice 1: re-anchor the charter and context on SSO, with a context gate test (FID-CTX-001..003, issue #1) | complete | fides, slice:1, minimal, context, FIDES-P0 | high |
| WI-0004 | Fides slice 2: trust-boundary model and the decision on what GitHub authentication proves (FID-TB-001..006, issue #1) | complete | fides, slice:2, minimal, trust-boundaries, decision | high |
| WI-0005 | Fides slice 3: executable acceptance tests before production code - invalid state/nonce, replay, wrong callback, revoked identity, expired session, repository access denial, provider outage (FID-TEST-001) | complete | fides, slice:3, minimal, testing, FIDES-P0 | high |
| WI-0006 | Fides slice 4: extensible provider model with GitHub as the only provider (FID-PRV-001..004, decision FID-D-002) | complete | fides, slice:4, minimal, providers, github | high |
| WI-0007 | Fides slice 5: pure F# code-for-token exchange core - authorization code with PKCE, single-use state, replay refusal (FID-EXC-001..005) | complete | fides, slice:5, minimal, exchange, pure-core | high |
| WI-0008 | Fides slice 6: AWS hosting adapter - Lambda behind API Gateway, client secret from Secrets Manager/SSM, infrastructure as code per environment (FID-HOST-001..003, FID-HOST-005) | complete | fides, slice:6, minimal, hosting, aws | high |
| WI-0009 | Fides slice 7: WASM client library and Arca token provider - sign-in, callback, session states, retention modes (FID-CLI-001..004) | complete | fides, slice:7, minimal, client, wasm | high |
| WI-0010 | Fides: shared hosting-adapter conformance suite (FID-TEST-002, FID-HOST-004) | complete | fides, conformance, hosting | medium |
| WI-0011 | (Deferred) Fides Azure hosting adapter - Azure Functions plus Key Vault (FID-HOST-004) | captured | fides, deferred, hosting, azure | low |
| WI-0012 | Fides: release workflow and echelon-registry entry for the client package and exchange artifact | complete | fides, release, registry | medium |
| WI-0013 | Make the Dokimos quality check pass: regenerate the test project as a dotnet test (xUnit) project so the gate gets TRX test evidence | complete | dokimos, ci | high |
| WI-0014 | Classify the exchange host's unexpected failures through Aegis (FID-EXC-005 Aegis clause) | complete | fides, aegis, blocked-external | medium |
| WI-0015 | Conditor's fsharp-nuget-library scaffold hides test output on failure: under bash -e, output=$(dotnet test ...) exits before echo | captured | ci, conditor, upstream | medium |
| WI-0016 | EchelonFoundry.Fides.Arca bridge package: Fides.Client token provider to Arca.TokenProvider | captured | fides, arca, blocked-external | medium |
| WI-0017 | Fides client: two threads could both start a refresh, and refresh tokens are single use (single-flight race) | complete | fides, client, defect | high |
| WI-0018 | Record Fides 0.1.0 in echelon-registry: release record, fides system entry, optional project binding | complete | fides, release, registry | high |
