namespace Fides

open System.Text.Json

/// Total readers for untrusted JSON: every failure is `None`, never an exception.
module JsonRead =
    /// Parses a JSON object and applies `read` to its root, or `None`.
    let object (read: JsonElement -> 'a option) (text: string) =
        try
            use document = JsonDocument.Parse text

            if document.RootElement.ValueKind = JsonValueKind.Object then
                read document.RootElement
            else
                None
        with :? JsonException ->
            None

    let private property (name: string) (element: JsonElement) =
        match element.TryGetProperty name with
        | true, value -> Some value
        | _ -> None

    /// A string property.
    let string (name: string) (element: JsonElement) =
        property name element
        |> Option.filter (fun v -> v.ValueKind = JsonValueKind.String)
        |> Option.bind (fun v -> v.GetString() |> Option.ofObj)

    /// An integer property.
    let int64 (name: string) (element: JsonElement) =
        property name element
        |> Option.filter (fun v -> v.ValueKind = JsonValueKind.Number)
        |> Option.bind (fun v ->
            match v.TryGetInt64() with
            | true, n -> Some n
            | _ -> None)

    /// Whether the property is present (with any value, including null).
    let has (name: string) (element: JsonElement) = (property name element).IsSome
