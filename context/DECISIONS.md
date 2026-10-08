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
