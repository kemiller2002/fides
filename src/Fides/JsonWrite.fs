namespace Fides

open System.Text.Json

/// Writes JSON text without reflection, so it trims and runs under WebAssembly.
module JsonWrite =
    /// A JSON value to write.
    type Value =
        | String of string
        | Number of int64
        | Bool of bool
        | Null
        | Object of (string * Value) list
        | Array of Value list

    let private quote (text: string) = "\"" + JsonEncodedText.Encode(text).ToString() + "\""

    let rec render (value: Value) =
        match value with
        | String text -> quote text
        | Number n -> string n
        | Bool b -> if b then "true" else "false"
        | Null -> "null"
        | Object pairs -> "{" + (pairs |> List.map (fun (k, v) -> quote k + ":" + render v) |> String.concat ",") + "}"
        | Array items -> "[" + (items |> List.map render |> String.concat ",") + "]"

    /// An object, omitting `None` members.
    let objectOf (pairs: (string * Value option) list) =
        Object(pairs |> List.choose (fun (k, v) -> v |> Option.map (fun v -> k, v)))
