# Fides decisions

Fides is the common Echelon authentication and single sign-on (SSO) identity boundary.

Material decisions use `DF-` records under `research/decisions/`. This compact
table is a navigation view, not a replacement for those records.

| Date | Decision | Status | Record |
|---|---|---|---|
| 2026-10-07 | Govern the repository with Praxis 3.7.2 and measure its operating value. | provisional | Not yet promoted to a `DF-` record |
| 2026-10-08 | The exchange is a pure, cloud-agnostic core behind a thin hosting adapter; AWS first, Azure possible. | accepted | DF-FIDES-2026-0001 |
| 2026-10-08 | GitHub is the only provider for now, behind an extensible provider model; Helix keeps its own sign-in; Arca never depends on Fides. | accepted | DF-FIDES-2026-0002 |
| 2026-10-08 | GitHub provider is a GitHub App with expiring user-to-server tokens and refresh, not an OAuth App. | accepted | DF-FIDES-2026-0003 |
| 2026-10-08 | The exchange is stateless: no session service, no server-held refresh tokens. | accepted | DF-FIDES-2026-0004 |
| 2026-10-08 | AWS account, region and domain are operator configuration; nothing is deployed from this repository; no secrets in the repository. | accepted | DF-FIDES-2026-0005 |
| 2026-10-08 | Interim distribution: Sigstore-attested GitHub release assets plus an echelon-registry entry until nuget.org is set up. | accepted | DF-FIDES-2026-0006 |
| 2026-10-08 | GitHub proves account control, the App authorization, token reach and identity; Fides owns client registration, redirect policy, state and PKCE, session states, revocation propagation and secret custody. | accepted | DF-FIDES-2026-0007 |
| 2026-10-08 | The client mirrors Arca 0.1.0's token-provider port in its own types; a packaged bridge waits for Conditor to install Arca's release assets. | accepted | DF-FIDES-2026-0008 |
| 2026-10-08 | Unexpected failures at the exchange host are classified through Aegis 1.0.0 with stable FIDES.* codes; exception messages never reach a sink; the client is not bound to Aegis yet. | accepted | DF-FIDES-2026-0009 |
| 2026-11-09 | Fides stays authentication-only; the enforcement point owns authorization; no central authorization service. | accepted | DF-FIDES-2026-0010 |
| 2026-11-09 | Organization membership is not held by Fides; tenants map to GitHub App installations and Arca repositories. | accepted | DF-FIDES-2026-0011 |
| 2026-11-09 | Keep the stateless exchange; its replay and revocation limits and the conditions for superseding DF-FIDES-2026-0004 are stated. | accepted | DF-FIDES-2026-0012 |
| 2026-11-09 | With browser-held user tokens, GitHub repository permission is the only enforced data boundary; one GitHub App per isolation domain. | superseded by DF-FIDES-2026-0016 | DF-FIDES-2026-0013 |
| 2026-11-09 | Non-human identities use installation tokens minted in their own runtime. | accepted | DF-FIDES-2026-0014 |
| 2026-11-09 | Restrict persistent refresh-token retention; design a sealed HttpOnly refresh cookie; require a strict CSP. | accepted | DF-FIDES-2026-0015 |
| 2026-11-11 | One shared Echelon GitHub App per environment while the owner is the only user, for single sign-on across applications; split per application before anyone else signs in; repository permission stays the only enforced data boundary. | accepted | DF-FIDES-2026-0016 |
