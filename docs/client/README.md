---
id: FIDES-CLIENT-GUIDE
title: Using the Fides client in an F# WebAssembly application
status: accepted
version: 1.3.0
created: 2026-10-08
updated: 2026-10-08
owners:
  - fides
related_documents:
  - docs/architecture/EXCHANGE-PROTOCOL.md
  - docs/architecture/TRUST-BOUNDARIES.md
  - research/decisions/DF-FIDES-2026-0008--client-mirrors-arca-token-provider-port.md
tags: [client, wasm, arca, limen]
provenance:
  contributions:
    EXE-20261008T093828040Z-3faec4b1:
      operations: [created]
      at: 2026-10-08T09:48:36.832Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Client guide and Arca bridge (WI-0009)"
    EXE-20261008T100753159Z-f178e0ab:
      operations: [modified]
      at: 2026-10-08T10:13:34.923Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Released assets: download, verify and install (WI-0012)"
    EXE-20261008T102052994Z-ff16f8b9:
      operations: [modified]
      at: 2026-10-08T10:23:41.930Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Install Fides through Conditor from echelon-current 1.5.0 (WI-0018)"
    EXE-20261008T103225377Z-c76772c6:
      operations: [modified]
      at: 2026-10-08T10:34:37.041Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Packaged Arca bridge through Conditor's feed (WI-0016)"
---

# Using the Fides client

`EchelonFoundry.Fides.Client` runs inside a Limen/Forma F# WebAssembly
application. It touches no browser API itself: the host supplies
`ClientPorts` (a `fetch` to the exchange, session and local storage,
navigation, address replacement, a `BroadcastChannel`, the clock and
`crypto.getRandomValues`). The same code runs in tests and in browser-wasm;
CI runs the full flow inside the trimmed WebAssembly runtime
(`scripts/verify-wasm.sh`).

## Installing

Until the packages are on nuget.org, each release ships them as
Sigstore-attested GitHub release assets (DF-FIDES-2026-0006), and
echelon-registry records them: `fides` is an optional project binding in
`echelon-current` 1.5.0 and later. Declare it in the application's
`conditor.json`:

```json
{ "id": "fides", "version": "0.1.0", "required": true }
```

and run `conditor upgrade --current` (or `conditor init`). Conditor proves
each package against the registry's sha256 before writing anything, places
it in `vendor/nuget` with a lock, and maps `EchelonFoundry.Fides`,
`EchelonFoundry.Fides.Client` and `EchelonFoundry.Fides.Hosting` to that feed
only. Then reference the packages by exact version:

```xml
<PackageReference Include="EchelonFoundry.Fides.Client" Version="0.1.0" />
```

Anyone can check provenance with
`gh attestation verify <file> --repo kemiller2002/fides`.

## Setting up

```fsharp
open Fides
open Fides.Client

let configuration =
    { Application = "chrona-production"            // as registered with the exchange
      Provider = ProviderId "github"               // configuration only (FID-PRV-004)
      ClientId = "Iv23li..."                       // the GitHub App's public client ID
      RedirectUri = "https://chrona.example/auth/callback" }

let catalog = ProviderCatalog.ofList [ GitHub.provider GitHub.githubDotCom ]
let fides = FidesClient.create configuration catalog ports
```

`ports.PostToExchange path body` must POST `body` to
`https://<exchange domain><path>` with `Content-Type: application/json`, and
return `Failed` rather than throw on a network error.

## Signing in

```fsharp
// On "Sign in with GitHub":
do! fides.SignIn MemoryOnly |> Async.Ignore

// On the callback page (the registered redirect URI):
match! fides.CompleteCallback query with
| CompletedSignIn identity -> // identity.Login, identity.Subject (stable id)
| outcome -> // CallbackOutcome.code outcome: state_invalid, state_expired, provider_denied, or an exchange refusal
```

`CompleteCallback` consumes the pending sign-in and removes `code` and
`state` from the address bar whatever the outcome.

## Retention (FID-CLI-002)

| Mode | Where tokens live | How to choose it |
|---|---|---|
| `MemoryOnly` (default) | Memory; gone on reload | `SignIn MemoryOnly` |
| `SessionScoped` | This tab's session storage | `SignIn SessionScoped` |
| `Persistent consent` | Local storage, across restarts | Only after showing the person what it means: `PersistenceConsent.givenAfter disclosureText` |

`SignOut` is the clear-token action: it clears every store first, tells the
other tabs, then revokes the token at GitHub through the exchange.

## Session states (FID-CLI-003)

`fides.State()` is one of `SignedOut`, `SigningIn`, `SignedIn identity`,
`Expired`, `Revoked`, `ProviderUnavailable`. Pass messages from the
`BroadcastChannel` to `fides.Receive`; they say only what happened, never a
token, and a tab still confirms with the provider before acting.

## Handing Arca its token provider (FID-CLI-001, ARCA-AUTH-001)

`fides.TokenProvider` has the shape of Arca's port,
`unit -> Async<Result<AccessToken, TokenUnavailable>>`, with the same four
failures as `Arca.TokenUnavailable`. Arca never references Fides; an
application that uses both references `EchelonFoundry.Fides.Arca`, which
depends on `EchelonFoundry.Arca.Core` 0.1.0 (installed by Conditor from its
verified release-asset feed, like Fides itself):

```fsharp
open Fides.Arca

let arcaTokens: Arca.TokenProvider = TokenBridge.ofClient fides
```

When Arca reports a `401` for the token, call `fides.ReportUnauthorized()`.
