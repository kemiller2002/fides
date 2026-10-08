---
id: FIDES-CLIENT-GUIDE
title: Using the Fides client in an F# WebAssembly application
status: accepted
version: 1.0.0
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
---

# Using the Fides client

`EchelonFoundry.Fides.Client` runs inside a Limen/Forma F# WebAssembly
application. It touches no browser API itself: the host supplies
`ClientPorts` (a `fetch` to the exchange, session and local storage,
navigation, address replacement, a `BroadcastChannel`, the clock and
`crypto.getRandomValues`). The same code runs in tests and in browser-wasm;
CI runs the full flow inside the trimmed WebAssembly runtime
(`scripts/verify-wasm.sh`).

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
failures as `Arca.TokenUnavailable`. Arca never references Fides; the
application bridges the two types where it composes them:

```fsharp
let arcaTokens (fides: FidesClient) : Arca.TokenProvider =
    fun () ->
        async {
            match! fides.TokenProvider() with
            | Ok token ->
                return
                    Arca.AccessToken.create (Fides.Secret.reveal token)
                    |> Result.mapError (fun _ -> Arca.TokenUnavailable.ProviderFailed "malformed token")
            | Error Fides.Client.TokenUnavailable.NoToken -> return Error Arca.TokenUnavailable.NoToken
            | Error Fides.Client.TokenUnavailable.Expired -> return Error Arca.TokenUnavailable.Expired
            | Error Fides.Client.TokenUnavailable.Revoked -> return Error Arca.TokenUnavailable.Revoked
            | Error(Fides.Client.TokenUnavailable.ProviderFailed reason) -> return Error(Arca.TokenUnavailable.ProviderFailed reason)
        }
```

When Arca reports a `401` for the token, call `fides.ReportUnauthorized()`.
A packaged bridge (`EchelonFoundry.Fides.Arca`) is planned once Conditor can
install Arca's release assets (DF-FIDES-2026-0008).
