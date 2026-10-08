---
id: PROJECT-CHARTER-fides
title: Fides Project Charter
status: accepted
version: 1.0.0
created: 2026-10-07
updated: 2026-10-08
provenance:
  contributions:
    EXE-20261008T084302418Z-58dc4016:
      operations: [modified]
      at: 2026-10-08T08:46:20.321Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Re-anchor the charter on SSO (issue #1)"
---

# Fides project charter

## Purpose

Fides is the common Echelon authentication and single sign-on (SSO) identity boundary.

Every Echelon application (Chrona first, then Summa and Signal) signs its
users in through Fides instead of building its own sign-in. Fides starts with
GitHub identity and GitHub repository access, because Arca, the shared data
layer, stores application data in GitHub and needs a GitHub token. GitHub is
the first provider behind an extensible provider model, not the permanent
architecture (DF-FIDES-2026-0002).

Fides has two parts:

1. **The exchange.** A small, stateless, server-side service that swaps an
   OAuth authorization code for a provider token, refreshes it and revokes
   it. It is the only place a client secret exists. It is a pure F# core
   behind a thin hosting adapter; AWS Lambda behind an HTTP API is the first
   host (DF-FIDES-2026-0001, DF-FIDES-2026-0004).
2. **The client.** An F# library that runs inside Limen/Forma WebAssembly
   applications. It starts sign-in, completes the callback, holds the session
   in the browser and hands Arca a token provider (FID-CLI-001).

## Intended users

- **People** who sign in to Echelon applications with their GitHub account.
- **Echelon applications**, registered with Fides as OAuth clients: each has
  its own allowed origins and exactly matched redirect URIs.
- **Arca**, which receives tokens only through its token-provider port and
  never depends on Fides (DF-FIDES-2026-0002).
- **The operator** (the repository owner), who registers the GitHub App,
  chooses the AWS account, region and domain, and holds the secrets.

## First bounded outcome

A person can sign in to Chrona with GitHub through Fides, and Chrona's Arca
storage receives an expiring, refreshable GitHub App user-to-server token
through the token provider. It is delivered as eight slices (WI-0003 to
WI-0010), each with executable tests, before Chrona's sign-in work starts.

## Included

- The GitHub App provider behind the provider model (FID-PRV).
- The pure code-for-token exchange core: PKCE, single-use state, exact
  redirect-URI allow-listing, refresh and revocation (FID-EXC).
- The AWS Lambda hosting adapter and its infrastructure template, as code
  and tests only (FID-HOST).
- The WebAssembly client and Arca token provider (FID-CLI).
- Security and acceptance tests, and an adapter conformance suite (FID-TEST).

## Excluded

- Deploying anything. The AWS account, region and domain are configuration
  that the operator supplies (DF-FIDES-2026-0005).
- A server-side session service or server-held refresh tokens
  (DF-FIDES-2026-0004).
- Identity providers other than GitHub, until a consumer needs one.
- Helix sign-in, which stays as it is for now (DF-FIDES-2026-0002).
- Application authorization. Each application decides what a signed-in user
  may do; GitHub decides what the token can reach (FID-TB-004).
- Secrets of any kind in the repository.

## Success criteria

- Every requirement in `docs/requirements/FIDES-REQUIREMENTS.md` is planned
  by an open work item or implemented and tested.
- The acceptance tests from issue #1 (invalid state, replay, wrong callback,
  revoked identity, expired session, repository access denial, provider
  outage) run against the real core and pass.
- A token, code, client secret or PKCE verifier never appears in a log,
  error, URL after the callback or stored record.
- A successor can continue from repository records without chat history.

## Constraints and assumptions

- Functional style: a pure core, with effects as explicit data or ports.
- GitHub App user-to-server tokens expire (8 hours) and are refreshed with a
  refresh token (6 months). Refresh needs the client secret, so it goes
  through the exchange (DF-FIDES-2026-0003).
- Packages are distributed as Sigstore-attested GitHub release assets with
  an echelon-registry entry until nuget.org is set up (DF-FIDES-2026-0006).

## Owners and decision authority

- Owner and decision authority: Kevin Miller (repository owner).
- Implementation: agents working under the Praxis protocol in this
  repository.
