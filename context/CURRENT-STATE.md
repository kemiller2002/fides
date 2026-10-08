# Fides current state

Fides is the common Echelon authentication and single sign-on (SSO) identity boundary.

## Repository status

- Praxis 3.7.2 governs the repository; Conditor installed the Echelon
  tooling (echelon-current 1.3.0, Ordo 1.5.0) and the F# NuGet library
  scaffold (WI-0001, WI-0013).
- Requirements, decisions and the dependency-ordered backlog are recorded
  (WI-0002): `docs/requirements/FIDES-REQUIREMENTS.md`,
  `docs/requirements/backlog-plan.md`, `research/decisions/`.
- The charter and this context are anchored on SSO (WI-0003, issue #1). A
  context gate test fails if they drift back to boilerplate.

## Observed facts

- The pure core has its provider model and GitHub App provider (WI-0006)
  and the exchange core and host-neutral service (WI-0007). All 30 exchange
  acceptance scenarios pass against the core.
- The AWS Lambda adapter (WI-0008) is code, tests and a CloudFormation
  template only: nothing is deployed. `docs/hosting/AWS.md` lists what the
  operator must provide. Its tests run the function in process over real
  HTTP against simulated GitHub, and run the built `bootstrap` against an
  emulated Lambda Runtime API.
- The acceptance scenarios (WI-0005) and the GitHub simulator live in
  `tests/Fides.Tests/Acceptance`.
- The user decided: GitHub only, through a GitHub App with expiring
  user-to-server tokens (DF-FIDES-2026-0003); a stateless exchange with no
  session service (DF-FIDES-2026-0004); AWS Lambda first, with account,
  region and domain as operator configuration and no deployment from here
  (DF-FIDES-2026-0005); interim distribution as attested GitHub release
  assets plus a registry entry (DF-FIDES-2026-0006).
- Arca's token-provider port is on arca main and released in
  EchelonFoundry.Arca.Core 0.1.0 (attested GitHub release assets). The Fides
  client mirrors it case for case (DF-FIDES-2026-0008); the packaged bridge
  (WI-0016) waits for Conditor to install Arca's release assets.
- The WebAssembly client (WI-0009) runs every callback and session acceptance
  scenario, and its full flow runs inside the trimmed browser-wasm runtime
  under Node in CI (`scripts/verify-wasm.sh`).
- The exchange host classifies unexpected failures through Aegis
  (`EchelonFoundry.Aegis.Core` 1.0.0 from nuget.org, WI-0014,
  DF-FIDES-2026-0009). Correction: an earlier entry here said Aegis was
  unpublished; that check had queried a package id that does not exist.
  Aegis 1.0.0 needs FSharp.Core 10.1.400, so building needs .NET SDK 10.0.400
  or later (`global.json`).

- The hosting-adapter conformance suite (WI-0010) runs every exchange
  scenario and 12 extra HTTP cases through an adapter and requires the
  core's exact status, body and headers; the AWS adapter conforms.

- Release 0.1.0 (WI-0012) ships EchelonFoundry.Fides, .Fides.Client and
  .Fides.Hosting plus the AWS Lambda package as Sigstore-attested GitHub
  release assets; nuget.org publication is dormant until `NUGET_USER` is set.
  echelon-registry records it (`releases/fides/0.1.0.release.json`) and
  offers `fides` as an optional project binding in `echelon-current` 1.5.0
  (WI-0018); Conditor installs it into a consumer's verified local feed.

## Active work

The minimal slices (WI-0003 to WI-0010) are done. Next: the packaged Arca bridge (WI-0016, now unblocked by
conditor#59) and the Conditor scaffold fix (WI-0015). The Azure adapter (WI-0011) stays
deferred.

## Largest decision-relevant unknown

The operator's GitHub App registration, AWS account, region and domain.
None blocks building and testing; all block deployment.

## Baseline

The pilot measurement plan (`docs/PILOT-MEASUREMENT-PLAN.md`) compares this
Praxis-governed build against a declared lightweight workflow; Praxis
telemetry records each slice's executions.

<!-- conditor:ordo-baseline:start -->
# Current State — Conditor Baseline

This is the initial greenfield baseline. It records what Conditor can prove before application implementation begins.

## Established facts

- The declared Echelon capabilities were planned and installed through their supported lifecycle contracts.
- Accepted requirement artifacts were materialized from immutable sources declared by `conditor.json`.
- Canonical execution contract: `none declared`.
- The generated `src/Fides/Library.fs` value is a scaffold placeholder, not a claim that the application domain has been modeled.

## Accepted requirement artifacts

- none declared

## Unknowns

- Application domain concepts have not yet been derived.
- Important legal state and illegal states have not yet been identified.
- Legal transitions, invariants, guards, capabilities, and effect obligations have not yet been established.
- Semantic feature boundaries and ownership are not yet known.

## Obligations before implementation expands

1. Read the canonical execution contract and its normative references.
2. Identify the smallest domain concepts and legal state required by the first meaningful vertical behavior.
3. Establish legal transitions, invariants, guards, capabilities, and explicit effects required by that behavior.
4. Update `SDE-MAP.md` and create feature manifests only when real semantic ownership is known.
5. Preserve unknowns explicitly rather than converting missing knowledge into assumptions.

## Next action

Execute the initial Praxis mission using the accepted governing inputs and establish the first evidence-backed Ordo semantic slice.
<!-- conditor:ordo-baseline:end -->
