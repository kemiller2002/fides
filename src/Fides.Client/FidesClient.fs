namespace Fides.Client

open System
open System.Threading.Tasks
open Fides

/// One browser store (session storage or local storage), as the host
/// provides it. Implementations must not throw.
type Storage =
    { Read: string -> Async<string option>
      Write: string -> string -> Async<unit>
      Remove: string -> Async<unit> }

/// What the host (a Limen kernel, or JavaScript interop) provides. The client
/// itself touches no browser API.
type ClientPorts =
    { /// POSTs a JSON body to the exchange path (`/v1/token` and so on) with
      /// `Content-Type: application/json`. Must not throw: a failure is `Failed`.
      PostToExchange: string -> string -> Async<HttpOutcome>
      /// This tab's session storage.
      TabStorage: Storage
      /// Local storage, shared by the origin's tabs.
      DeviceStorage: Storage
      /// Sends the browser to a URL (the provider's authorize page).
      Navigate: string -> Async<unit>
      /// Replaces the address bar's URL without navigating (removes `code` and `state`).
      ReplaceAddress: string -> Async<unit>
      /// Tells the origin's other tabs something; never carries a token.
      Broadcast: string -> unit
      Now: unit -> DateTimeOffset
      /// Cryptographically secure random bytes (`crypto.getRandomValues`).
      RandomBytes: int -> byte array }

/// How sign-out ended. Local state is cleared whatever the result.
type SignOutResult =
    | RevokedAtProvider
    | RevocationFailed of reason: string
    | NothingToRevoke

/// A signed-in (or not) application's handle on Fides.
type FidesClient =
    { /// Starts a sign-in: stores the pending sign-in for this tab and
      /// navigates to the provider. Memory-only retention unless chosen.
      SignIn: Retention -> Async<Result<unit, string>>
      /// Completes the callback from the provider's redirect query.
      CompleteCallback: (string * string) list -> Async<CallbackOutcome>
      /// Restores a stored session on page load.
      Restore: unit -> Async<SessionState>
      State: unit -> SessionState
      /// The token provider Arca consumes (ARCA-AUTH-001).
      TokenProvider: TokenProvider
      /// The application got a 401 for the token: the session is revoked.
      ReportUnauthorized: unit -> Async<unit>
      /// Clears every store, tells other tabs, and revokes at the provider.
      SignOut: unit -> Async<SignOutResult>
      /// Handles a message another tab broadcast.
      Receive: string -> Async<unit> }

module FidesClient =
    let private pendingKey (configuration: ClientConfiguration) = $"fides.{configuration.Application}.pending"
    let private sessionKey (configuration: ClientConfiguration) = $"fides.{configuration.Application}.session"

    /// The cross-tab messages. They carry no token, only what happened.
    let message (configuration: ClientConfiguration) (event: string) = $"fides:{configuration.Application}:{event}"

    let create (configuration: ClientConfiguration) (catalog: ProviderCatalog) (ports: ClientPorts) : FidesClient =
        let mutable session: Session option = None
        let mutable state = SignedOut
        let mutable refreshing: Task<Result<AccessToken, TokenUnavailable>> option = None

        let key = sessionKey configuration
        let broadcast event = ports.Broadcast(message configuration event)

        let storeFor =
            function
            | MemoryOnly -> None
            | SessionScoped -> Some ports.TabStorage
            | Persistent _ -> Some ports.DeviceStorage

        let clearStores () =
            async {
                do! ports.TabStorage.Remove key
                do! ports.DeviceStorage.Remove key
            }

        let save (next: Session) =
            async {
                session <- Some next
                state <- SignedIn next.Identity
                do! clearStores ()

                match storeFor next.Retention with
                | Some store -> do! store.Write key (Codec.encodeSession next)
                | None -> ()
            }

        let forget (next: SessionState) =
            async {
                session <- None
                state <- next
                do! clearStores ()
            }

        let readStored () =
            async {
                let! inTab = ports.TabStorage.Read key
                let! onDevice = ports.DeviceStorage.Read key
                return [ inTab; onDevice ] |> List.choose id |> List.tryPick Codec.decodeSession
            }

        let refresh (current: Session) =
            async {
                let request = Protocol.encodeRefreshRequest { Application = configuration.Application; RefreshToken = Secret.reveal current.Grant.RefreshToken }
                let! outcome = ports.PostToExchange Protocol.RefreshPath request

                match Tokens.refreshed current outcome with
                | Tokens.Renewed next ->
                    do! save next
                    return Ok next.Grant.AccessToken
                | Tokens.Rejected ->
                    // Refresh tokens are single use: another tab may already have
                    // rotated this one and stored the result.
                    let! stored = readStored ()

                    match stored with
                    | Some other when other.Grant.RefreshToken <> current.Grant.RefreshToken ->
                        match Tokens.decide (ports.Now()) (SignedIn other.Identity) (Some other) with
                        | Tokens.Use token ->
                            session <- Some other
                            state <- SignedIn other.Identity
                            return Ok token
                        | _ ->
                            do! forget SessionState.Revoked
                            broadcast "revoked"
                            return Error TokenUnavailable.Revoked
                    | _ ->
                        do! forget SessionState.Revoked
                        broadcast "revoked"
                        return Error TokenUnavailable.Revoked
                | Tokens.Unreachable reason ->
                    state <- ProviderUnavailable
                    return Error(TokenUnavailable.ProviderFailed reason)
            }

        let token () =
            async {
                match Tokens.decide (ports.Now()) state session with
                | Tokens.Use token -> return Ok token
                | Tokens.Unavailable TokenUnavailable.Expired when session.IsSome ->
                    do! forget SessionState.Expired
                    return Error TokenUnavailable.Expired
                | Tokens.Unavailable reason -> return Error reason
                | Tokens.Refresh current ->
                    // At most one refresh at a time: concurrent callers share it.
                    let task =
                        match refreshing with
                        | Some running -> running
                        | None ->
                            let running = Async.StartImmediateAsTask(refresh current)
                            refreshing <- Some running
                            running

                    let! result = Async.AwaitTask task

                    match refreshing with
                    | Some running when Object.ReferenceEquals(running, task) -> refreshing <- None
                    | _ -> ()

                    return result
            }

        let signIn (retention: Retention) =
            async {
                match ProviderCatalog.tryFind configuration.Provider catalog with
                | None -> return Error "the configured provider is not available"
                | Some provider ->
                    let pending, url = SignIn.start configuration provider retention (ports.RandomBytes 64) (ports.Now())
                    do! ports.TabStorage.Write (pendingKey configuration) (Codec.encodePending pending)
                    state <- SigningIn
                    do! ports.Navigate url
                    return Ok()
            }

        let completeCallback (query: (string * string) list) =
            async {
                let pendingKey = pendingKey configuration
                let! stored = ports.TabStorage.Read pendingKey
                // Consumed whatever happens next: a callback is never replayable.
                do! ports.TabStorage.Remove pendingKey
                // The code and state leave the address bar before anything else.
                do! ports.ReplaceAddress configuration.RedirectUri
                let pending = stored |> Option.bind Codec.decodePending
                let previous = if session.IsSome then state else SignedOut

                match SignIn.callback configuration pending query (ports.Now()) with
                | SignIn.Refuse outcome ->
                    state <- previous
                    return outcome
                | SignIn.Exchange request ->
                    state <- SigningIn
                    let! outcome = ports.PostToExchange Protocol.TokenPath (Protocol.encodeTokenRequest request)
                    let retention = pending |> Option.map _.Retention |> Option.defaultValue MemoryOnly

                    match SignIn.completed retention outcome with
                    | Ok next ->
                        do! save next
                        broadcast "signed_in"
                        return CompletedSignIn next.Identity
                    | Error refusal ->
                        state <- (if refusal = Protocol.ProviderUnavailable then ProviderUnavailable else previous)
                        return ExchangeRefused refusal
            }

        let restore () =
            async {
                match session with
                | Some _ -> return state
                | None ->
                    let! stored = readStored ()

                    match stored with
                    | Some found when found.Grant.RefreshTokenExpiresAt > ports.Now() ->
                        session <- Some found
                        state <- SignedIn found.Identity
                    | Some _ -> do! forget SessionState.Expired
                    | None -> ()

                    return state
            }

        let signOut () =
            async {
                let had = session
                do! forget SignedOut
                broadcast "signed_out"

                match had with
                | None -> return NothingToRevoke
                | Some previous ->
                    let request = Protocol.encodeRevokeRequest { Application = configuration.Application; AccessToken = Secret.reveal previous.Grant.AccessToken }
                    let! outcome = ports.PostToExchange Protocol.RevokePath request

                    match outcome with
                    | Responded { Status = 204 } -> return RevokedAtProvider
                    | Responded response -> return RevocationFailed(Protocol.decodeRefusal response.Body |> Option.map Protocol.code |> Option.defaultValue "unknown")
                    | Failed _ -> return RevocationFailed(Protocol.code Protocol.ProviderUnavailable)
            }

        let receive (text: string) =
            async {
                if text = message configuration "signed_out" then
                    do! forget SignedOut
                elif text = message configuration "revoked" then
                    do! forget SessionState.Revoked
                elif text = message configuration "signed_in" && session.IsNone then
                    // Only a hint: the session is read from the store, never from the message.
                    let! _ = restore ()
                    ()
            }

        { SignIn = signIn
          CompleteCallback = completeCallback
          Restore = restore
          State = fun () -> state
          TokenProvider = token
          ReportUnauthorized =
            fun () ->
                async {
                    do! forget SessionState.Revoked
                    broadcast "revoked"
                }
          SignOut = signOut
          Receive = receive }
