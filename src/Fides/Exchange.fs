namespace Fides

open Fides.Protocol

/// The code-for-token exchange: pure, stateless and cloud-agnostic
/// (FID-EXC-001..005, DF-FIDES-2026-0001, DF-FIDES-2026-0004). It validates
/// everything it can before the provider is called, and returns either the
/// whole result or a typed refusal, never a partial session.
module Exchange =
    /// What a caller asks the exchange to do, as it arrived on the wire.
    type Operation =
        | ExchangeCode of TokenRequest
        | RefreshTokens of RefreshRequest
        | RevokeToken of RevokeRequest

    type Outcome =
        | SignedIn of TokenGrant * Identity
        | Refreshed of TokenGrant
        | Revoked

    /// Which call a provider refusal came from; the same provider answer means
    /// different things at different steps.
    type private Step =
        | CodeStep
        | RefreshStep
        | IdentityStep
        | RepositoryStep
        | RevocationStep

    let private refusalFor (step: Step) (refusal: ProviderRefusal) =
        match step, refusal with
        | _, Unavailable -> ProviderUnavailable
        | _, ClientCredentialsRejected -> ConfigurationUnavailable
        | CodeStep, ProviderRefusal.CodeRejected -> Refusal.CodeRejected
        | RefreshStep, ProviderRefusal.RefreshRejected -> Refusal.RefreshRejected
        | (IdentityStep | RepositoryStep), TokenRejected -> IdentityRevoked
        | _ -> ProviderContractViolation

    /// The registered application, if the request names one.
    let registered (configuration: Configuration) (application: string) =
        configuration.Applications.TryFind(ApplicationId application)

    /// The origin must be exactly one of the application's registered origins
    /// (FID-HOST-005).
    let originAllowed (application: Application) (origin: string option) =
        match origin with
        | Some origin -> application.Origins |> List.contains origin
        | None -> false

    /// The redirect URI must be exactly one of the application's registered
    /// URIs: ordinal equality, no normalisation (FID-TB-003).
    let redirectAllowed (application: Application) (redirectUri: string) =
        application.RedirectUris |> List.exists (fun registered -> System.String.Equals(registered, redirectUri, System.StringComparison.Ordinal))

    let private check condition refusal = if condition then Ok() else Error refusal

    let private validated (configuration: Configuration) origin name =
        match registered configuration name with
        | None -> Error UnknownApplication
        | Some application ->
            check (originAllowed application origin) OriginNotAllowed
            |> Result.bind (fun () ->
                match configuration.Catalog.TryFind application.Provider, configuration.Clients.TryFind application.Provider with
                | Some provider, Some client -> Ok(application, provider, client)
                | _ -> Error ConfigurationUnavailable)

    open Effect

    let private withCredentials (client: ProviderClient) (next: ClientCredentials -> Effect<Result<'a, Refusal>>) =
        effect {
            match! readSecret client.ClientSecret with
            | Error() -> return Error ConfigurationUnavailable
            | Ok secret -> return! next { ClientId = client.ClientId; ClientSecret = secret }
        }

    /// Revokes a token the exchange will not return, then refuses. A token that
    /// is not handed out must not stay usable. Revocation is best effort: its
    /// own failure does not change the refusal.
    let private discard provider credentials token refusal =
        Provider.revoke provider credentials token |> map (fun _ -> Error refusal)

    let private signIn (application: Application) provider credentials (request: TokenRequest) verifier =
        effect {
            match! Provider.exchangeCode provider credentials (Secret.create request.Code) verifier request.RedirectUri with
            | Error refusal -> return Error(refusalFor CodeStep refusal)
            | Ok grant ->
                match! Provider.identify provider grant.AccessToken with
                | Error refusal -> return! discard provider credentials grant.AccessToken (refusalFor IdentityStep refusal)
                | Ok identity ->
                    match application.RequiredRepository with
                    | None -> return Ok(SignedIn(grant, identity))
                    | Some repository ->
                        match! Provider.repositoryAccess provider grant.AccessToken repository with
                        | Ok Readable -> return Ok(SignedIn(grant, identity))
                        | Ok NotReadable -> return! discard provider credentials grant.AccessToken RepositoryAccessDenied
                        | Error refusal -> return! discard provider credentials grant.AccessToken (refusalFor RepositoryStep refusal)
        }

    /// Runs one operation. `origin` is the request's `Origin` header.
    let run (configuration: Configuration) (origin: string option) (operation: Operation) : Effect<Result<Outcome, Refusal>> =
        match operation with
        | ExchangeCode request ->
            let prepared =
                validated configuration origin request.Application
                |> Result.bind (fun (application, provider, client) ->
                    check (redirectAllowed application request.RedirectUri) RedirectUriNotAllowed
                    |> Result.bind (fun () -> Pkce.parseVerifier request.CodeVerifier |> Result.mapError (fun () -> InvalidCodeVerifier))
                    |> Result.map (fun verifier -> application, provider, client, verifier))

            match prepared with
            | Error refusal -> Done(Error refusal)
            | Ok(application, provider, client, verifier) ->
                withCredentials client (fun credentials -> signIn application provider credentials request verifier)
        | RefreshTokens request ->
            match validated configuration origin request.Application with
            | Error refusal -> Done(Error refusal)
            | Ok(_, provider, client) ->
                withCredentials client (fun credentials ->
                    Provider.refresh provider credentials (Secret.create request.RefreshToken)
                    |> map (Result.map Refreshed >> Result.mapError (refusalFor RefreshStep)))
        | RevokeToken request ->
            match validated configuration origin request.Application with
            | Error refusal -> Done(Error refusal)
            | Ok(_, provider, client) ->
                withCredentials client (fun credentials ->
                    Provider.revoke provider credentials (Secret.create request.AccessToken)
                    |> map (Result.map (fun () -> Revoked) >> Result.mapError (refusalFor RevocationStep)))
