# Fides

Fides is the common Echelon authentication and single sign-on (SSO) identity boundary.

Echelon applications sign their users in through Fides. It starts with
GitHub, using a GitHub App and its expiring user-to-server tokens, behind a
provider model that can take other providers later. The purpose, users and
scope are in [`PROJECT-CHARTER.md`](PROJECT-CHARTER.md); the requirements are in
[`docs/requirements/FIDES-REQUIREMENTS.md`](docs/requirements/FIDES-REQUIREMENTS.md).

| Part | What it does |
|---|---|
| Exchange | Stateless server-side code-for-token exchange, refresh and revocation. The only holder of the client secret. Pure F# core, thin hosting adapter, AWS Lambda first. |
| Client | F# library for Limen/Forma WebAssembly applications: sign-in, callback, session, and the token provider that Arca consumes. |

Nothing is deployed from this repository. What the operator must supply to
deploy is listed in the hosting documentation once the AWS adapter lands
(WI-0008).

## Start here

1. Read [`AGENTS.md`](AGENTS.md) and [`BOOTSTRAP.md`](BOOTSTRAP.md).
2. Read [`PROJECT-CHARTER.md`](PROJECT-CHARTER.md) and
   [`context/CURRENT-STATE.md`](context/CURRENT-STATE.md).
3. Pick up the next work item with `./praxis work ready`.

## Build and test

```bash
dotnet build Fides.slnx -c Release
dotnet test Fides.slnx -c Release
```

## Local operating commands

```bash
./praxis work start --id WI-NNNN --occurred-at TIMESTAMP --type task
./praxis work context WI-NNNN
./praxis status
./praxis registry build
./praxis validate
```

`work context` reports legal actions and completion evidence. Validation
errors include repair instructions; use `./praxis validate --json` for
machine-readable output. Complete work with explicit evidence paths as
described in `docs/work-protocol.md`.
