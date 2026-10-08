namespace Fides.Client

open System
open Fides

/// When the token provider can hand out a token and when it must refresh, as
/// pure decisions (FID-CLI-001, FID-CLI-003).
module Tokens =
    /// A token is refreshed this long before it expires, so a caller never
    /// gets one that dies mid-request.
    let refreshMargin = TimeSpan.FromMinutes 5.

    type Decision =
        | Use of AccessToken
        | Refresh of Session
        | Unavailable of TokenUnavailable

    /// What to do with the session for a token request now.
    let decide (now: DateTimeOffset) (state: SessionState) (session: Session option) =
        match session with
        | None ->
            match state with
            | SessionState.Revoked -> Unavailable TokenUnavailable.Revoked
            | SessionState.Expired -> Unavailable TokenUnavailable.Expired
            | _ -> Unavailable TokenUnavailable.NoToken
        | Some session when session.Grant.AccessTokenExpiresAt - refreshMargin > now -> Use session.Grant.AccessToken
        | Some session when session.Grant.RefreshTokenExpiresAt <= now -> Unavailable TokenUnavailable.Expired
        | Some session -> Refresh session

    /// How a refresh ended.
    type RefreshResult =
        /// New tokens; the session continues.
        | Renewed of Session
        /// The grant is gone: revoked, or its refresh token already used elsewhere.
        | Rejected
        /// The exchange or provider failed; keep the session and retry later.
        | Unreachable of reason: string

    /// Reads the exchange's answer to `/v1/refresh`.
    let refreshed (session: Session) (outcome: HttpOutcome) =
        match outcome with
        | Failed _ -> Unreachable(Protocol.code Protocol.ProviderUnavailable)
        | Responded { Status = 200; Body = body } ->
            match Protocol.decodeGrant body with
            | Some(grant, _) -> Renewed { session with Grant = grant }
            | None -> Unreachable(Protocol.code Protocol.ProviderContractViolation)
        | Responded response ->
            match Protocol.decodeRefusal response.Body with
            | Some Protocol.RefreshRejected -> Rejected
            | Some refusal -> Unreachable(Protocol.code refusal)
            | None -> Unreachable(Protocol.code Protocol.ProviderUnavailable)
