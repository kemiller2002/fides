namespace Fides

open System

/// Identifies a registered application, such as `chrona-production`.
type ApplicationId = ApplicationId of string

/// An application registered with Fides as an OAuth client (FID-TB-003).
type Application =
    { Id: ApplicationId
      Provider: ProviderId
      /// Exact origins (`scheme://host[:port]`) allowed to call the exchange.
      Origins: string list
      /// Exact redirect URIs, compared by ordinal equality.
      RedirectUris: string list
      /// If set, sign-in requires the new token to read this repository.
      RequiredRepository: RepositoryName option }

/// The exchange's OAuth client at one provider. The secret is a reference
/// into the host's secret store, never the secret itself.
type ProviderClient =
    { ClientId: string
      ClientSecret: SecretReference }

/// Everything the exchange needs besides the request. It holds no secret.
type Configuration =
    { Applications: Map<ApplicationId, Application>
      Clients: Map<ProviderId, ProviderClient>
      Catalog: ProviderCatalog }

/// Reads and validates the operator's configuration document.
module Configuration =
    let private absolute (value: string) =
        match Uri.TryCreate(value, UriKind.Absolute) with
        | true, NonNull uri -> Some uri
        | _ -> None

    let private isLoopback (uri: Uri) =
        uri.Scheme = "http" && (uri.Host = "localhost" || uri.Host = "127.0.0.1" || uri.Host = "[::1]")

    let private acceptableScheme allowLoopback (uri: Uri) =
        uri.Scheme = "https" || (allowLoopback && isLoopback uri)

    /// An origin is `scheme://host[:port]` exactly as a browser sends it: no
    /// path, query, fragment, userinfo or trailing slash.
    let validOrigin allowLoopback (value: string) =
        match absolute value with
        | Some uri ->
            acceptableScheme allowLoopback uri
            && uri.UserInfo = ""
            && uri.PathAndQuery = "/"
            && uri.Fragment = ""
            && value = uri.GetLeftPart(UriPartial.Authority)
        | None -> false

    /// A redirect URI is absolute https (or loopback http when allowed), with
    /// no fragment, no userinfo and no wildcard.
    let validRedirectUri allowLoopback (value: string) =
        match absolute value with
        | Some uri ->
            acceptableScheme allowLoopback uri
            && uri.UserInfo = ""
            && uri.Fragment = ""
            && not (value.Contains '*')
            && not (value.Contains '#')
        | None -> false

    let private validId (value: string) =
        value.Length > 0
        && value.Length <= 64
        && value |> Seq.forall (fun c -> Char.IsAsciiLetterLower c || Char.IsAsciiDigit c || c = '-')

    let private application allowLoopback (catalog: ProviderCatalog) (clients: Map<ProviderId, ProviderClient>) element =
        let id = JsonRead.string "id" element |> Option.defaultValue ""
        let provider = JsonRead.string "provider" element |> Option.defaultValue ""
        let origins = JsonRead.strings "origins" element |> Option.defaultValue []
        let redirects = JsonRead.strings "redirectUris" element |> Option.defaultValue []
        let required = JsonRead.string "requiredRepository" element
        let at = if id = "" then "an application" else $"application '{id}'"

        let problems =
            [ if not (validId id) then $"{at}: id must be 1-64 characters of a-z, 0-9 and -"
              if not (catalog.ContainsKey(ProviderId provider)) then $"{at}: provider '{provider}' is not available"
              elif not (clients.ContainsKey(ProviderId provider)) then $"{at}: provider '{provider}' has no client configured"
              if origins.IsEmpty then $"{at}: at least one origin is required"
              for o in origins do
                  if not (validOrigin allowLoopback o) then $"{at}: origin '{o}' is not an exact https origin"
              if redirects.IsEmpty then $"{at}: at least one redirect URI is required"
              for r in redirects do
                  if not (validRedirectUri allowLoopback r) then $"{at}: redirect URI '{r}' is not an absolute https URI without fragment"
              match required |> Option.map RepositoryName.parse with
              | Some(Error()) -> $"{at}: requiredRepository must be owner/name"
              | _ -> () ]

        match problems, required |> Option.map RepositoryName.parse with
        | [], (None | Some(Ok _) as repository) ->
            Ok
                { Id = ApplicationId id
                  Provider = ProviderId provider
                  Origins = origins
                  RedirectUris = redirects
                  RequiredRepository =
                    match repository with
                    | Some(Ok name) -> Some name
                    | _ -> None }
        | problems, _ -> Error problems

    let private client (name: string, element: System.Text.Json.JsonElement) =
        match JsonRead.string "clientId" element, JsonRead.string "clientSecret" element with
        | Some id, Some reference when id.Length > 0 && reference.Length > 0 ->
            Ok(ProviderId name, { ClientId = id; ClientSecret = SecretReference reference })
        | _ -> Error [ $"provider '{name}': clientId and clientSecret (a secret-store reference) are required" ]

    let private collect (results: Result<'a, string list> list) =
        let values = results |> List.choose (function Ok v -> Some v | Error _ -> None)
        let errors = results |> List.collect (function Error e -> e | Ok _ -> [])
        if errors.IsEmpty then Ok values else Error errors

    /// Parses the configuration document. Every problem is reported, not just
    /// the first. The document holds secret references, never secrets.
    ///
    /// ```json
    /// { "allowLoopback": false,
    ///   "providers": { "github": { "clientId": "Iv23...", "clientSecret": "arn:aws:secretsmanager:..." } },
    ///   "applications": [ { "id": "chrona-production", "provider": "github",
    ///                       "origins": ["https://chrona.example"],
    ///                       "redirectUris": ["https://chrona.example/auth/callback"],
    ///                       "requiredRepository": "owner/name" } ] }
    /// ```
    let parse (catalog: ProviderCatalog) (document: string) : Result<Configuration, string list> =
        let read root =
            let allowLoopback = JsonRead.bool "allowLoopback" root |> Option.defaultValue false
            let clients = JsonRead.members "providers" root |> Option.defaultValue [] |> List.map client |> collect

            match clients with
            | Error e -> Some(Error e)
            | Ok clients ->
                let clientMap = Map.ofList clients

                let applications =
                    JsonRead.array "applications" root
                    |> Option.defaultValue []
                    |> List.map (application allowLoopback catalog clientMap)
                    |> collect

                match applications with
                | Error e -> Some(Error e)
                | Ok [] -> Some(Error [ "at least one application is required" ])
                | Ok applications ->
                    let duplicates =
                        applications |> List.countBy _.Id |> List.filter (fun (_, n) -> n > 1) |> List.map fst

                    match duplicates with
                    | [] ->
                        Some(
                            Ok
                                { Applications = applications |> List.map (fun a -> a.Id, a) |> Map.ofList
                                  Clients = clientMap
                                  Catalog = catalog }
                        )
                    | ids -> Some(Error [ for ApplicationId id in ids -> $"application '{id}' is registered twice" ])

        JsonRead.object read document
        |> Option.defaultValue (Error [ "the configuration is not a JSON object" ])
