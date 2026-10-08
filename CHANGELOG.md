# Changelog

## 0.1.0 (2026-10-08)

First release: the minimal single sign-on slices (WI-0003 to WI-0010, WI-0017).
Distributed as Sigstore-attested GitHub release assets; nuget.org publication
starts once `NUGET_USER` is set (DF-FIDES-2026-0006).

- **EchelonFoundry.Fides**: the pure core. Provider model with the GitHub App
  provider (expiring user-to-server tokens with refresh), PKCE S256, the
  stateless code-for-token exchange with exact redirect-URI and origin
  allow-lists, and the host-neutral HTTP service. Every effect is data.
- **EchelonFoundry.Fides.Client**: the WebAssembly client. Sign-in, callback,
  typed session states, retention modes, single-flight refresh, cross-tab
  messages, and a token provider mirroring Arca 0.1.0's port.
- **EchelonFoundry.Fides.Hosting**: the cloud-neutral hosting runtime.
- **fides-exchange-linux-arm64.zip**: the AWS Lambda package
  (`provided.al2023`, arm64). Deploy it with
  `infrastructure/aws/fides-exchange.template.json` as `docs/hosting/AWS.md`
  describes. Nothing is deployed by the release.
