namespace Fides.Client

open Fides
open Fides.JsonWrite

/// The client's stored records. They hold tokens or a verifier, so they go
/// only to the store the retention mode names, never anywhere else.
module Codec =
    let private retentionCode =
        function
        | MemoryOnly -> "memory"
        | SessionScoped -> "session"
        | Persistent _ -> "persistent"

    let private retentionOf (code: string) (disclosure: string option) =
        match code, disclosure with
        | "memory", _ -> Some MemoryOnly
        | "session", _ -> Some SessionScoped
        | "persistent", Some text -> PersistenceConsent.givenAfter text |> Result.toOption |> Option.map Persistent
        | _ -> None

    let private disclosureOf =
        function
        | Persistent consent -> Some(String(PersistenceConsent.disclosure consent))
        | _ -> None

    let encodePending (pending: PendingSignIn) =
        render (
            objectOf
                [ "state", Some(String pending.State)
                  "verifier", Some(String(Secret.reveal pending.Verifier))
                  "createdAt", Some(String(Protocol.formatInstant pending.CreatedAt))
                  "retention", Some(String(retentionCode pending.Retention))
                  "disclosure", disclosureOf pending.Retention ]
        )

    let decodePending (text: string) =
        text
        |> JsonRead.object (fun root ->
            match
                JsonRead.string "state" root,
                JsonRead.string "verifier" root |> Option.bind (Pkce.parseVerifier >> Result.toOption),
                JsonRead.string "createdAt" root |> Option.bind Protocol.parseInstant,
                JsonRead.string "retention" root |> Option.bind (fun r -> retentionOf r (JsonRead.string "disclosure" root))
            with
            | Some state, Some verifier, Some createdAt, Some retention ->
                Some { State = state; Verifier = verifier; CreatedAt = createdAt; Retention = retention }
            | _ -> None)

    /// A session: the exchange's token response plus the retention mode.
    let encodeSession (session: Session) =
        let grant = Protocol.encodeGrant session.Grant (Some session.Identity)

        render (
            objectOf
                [ "retention", Some(String(retentionCode session.Retention))
                  "disclosure", disclosureOf session.Retention ]
        )
        |> fun meta -> $"{{\"grant\":{grant},\"meta\":{meta}}}"

    let decodeSession (text: string) =
        text
        |> JsonRead.object (fun root ->
            match JsonRead.child "grant" root, JsonRead.child "meta" root with
            | Some grant, Some meta ->
                match
                    Protocol.decodeGrant (grant.GetRawText()),
                    JsonRead.string "retention" meta |> Option.bind (fun r -> retentionOf r (JsonRead.string "disclosure" meta))
                with
                | Some(grant, Some identity), Some retention -> Some { Grant = grant; Identity = identity; Retention = retention }
                | _ -> None
            | _ -> None)
