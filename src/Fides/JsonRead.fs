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

    /// A boolean property.
    let bool (name: string) (element: JsonElement) =
        property name element
        |> Option.bind (fun v ->
            match v.ValueKind with
            | JsonValueKind.True -> Some true
            | JsonValueKind.False -> Some false
            | _ -> None)

    /// A nested object property.
    let child (name: string) (element: JsonElement) =
        property name element |> Option.filter (fun v -> v.ValueKind = JsonValueKind.Object)

    /// The elements of an array property.
    let array (name: string) (element: JsonElement) =
        property name element
        |> Option.filter (fun v -> v.ValueKind = JsonValueKind.Array)
        |> Option.map (fun v -> v.EnumerateArray() |> List.ofSeq)

    /// An array of strings; `None` if any element is not a string.
    let strings (name: string) (element: JsonElement) =
        array name element
        |> Option.bind (fun items ->
            let values = items |> List.choose (fun i -> if i.ValueKind = JsonValueKind.String then i.GetString() |> Option.ofObj else None)
            if values.Length = items.Length then Some values else None)

    /// The members of an object property, by name.
    let members (name: string) (element: JsonElement) =
        child name element |> Option.map (fun v -> v.EnumerateObject() |> Seq.map (fun p -> p.Name, p.Value) |> List.ofSeq)
