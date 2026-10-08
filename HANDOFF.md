# Fides handoff

Fides is the common Echelon authentication and single sign-on (SSO) identity boundary.

## Objective

Build Fides's minimal slices (WI-0003 to WI-0010) so Chrona can sign in
with GitHub and hand Arca a token, then release the packages (WI-0012).

## Current state

See [`context/CURRENT-STATE.md`](context/CURRENT-STATE.md) and the queue
(`./praxis work ready`, `.ros/work/queue.md`).

## Validation

```bash
dotnet build Fides.slnx -c Release
dotnet test Fides.slnx -c Release
./praxis registry check
./praxis validate
```

## Continuity

An executor session is disposable. Before handing off, commit and push the
work, record `./praxis work checkpoint --id ID --occurred-at NOW --summary ...
--next-action ...`, and push the `.ros/` state. A successor reads
`./praxis work context ID --text` and takes over with `./praxis work continue`.

## Unresolved questions

1. The operator's GitHub App registration, AWS account, region and domain
   (needed to deploy, not to build).
2. Whether the client should also report to Aegis once a browser sink
   exists (DF-FIDES-2026-0009 keeps Aegis at the exchange host for now).

## Next action

Take the next ready slice in backlog order.
