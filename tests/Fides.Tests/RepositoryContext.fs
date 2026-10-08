/// FID-CTX-003: the repository's authoritative context must keep stating what
/// Fides is. Any of the context files losing the purpose statement, or picking
/// up the scaffold's boilerplate, is a finding.
module Fides.Tests.RepositoryContext

/// The one-sentence purpose every context file states verbatim (FID-CTX-001).
let purposeStatement =
    "Fides is the common Echelon authentication and single sign-on (SSO) identity boundary."

/// The context files that must agree on the purpose (FID-CTX-002).
let contextFiles =
    [ "PROJECT-CHARTER.md"
      "README.md"
      "HANDOFF.md"
      "context/ARCHITECTURE.md"
      "context/CURRENT-STATE.md"
      "context/DECISIONS.md" ]

/// The readable work queue, which must not carry the boilerplate either.
let queueFile = ".ros/work/queue.md"

/// Phrases from the generated greenfield scaffold that describe a different,
/// undefined product. Their return means the context drifted (issue #1).
let boilerplateMarkers =
    [ "communication problem"
      "Communication Engineering"
      "Not yet established"
      "Not yet selected"
      "Not yet assigned"
      "greenfield pilot"
      "Define the practical problem this project will solve" ]

/// Why a context file fails the gate.
type Finding =
    | MissingPurpose of path: string
    | Boilerplate of path: string * marker: string

let private boilerplateIn (path: string) (content: string) =
    boilerplateMarkers
    |> List.filter (fun marker -> content.Contains(marker, System.StringComparison.OrdinalIgnoreCase))
    |> List.map (fun marker -> Boilerplate(path, marker))

/// Findings for one context file.
let contextFindings (path: string, content: string) =
    let purpose = if content.Contains purposeStatement then [] else [ MissingPurpose path ]
    purpose @ boilerplateIn path content

/// Findings across the context files and the work queue.
let findings (contexts: (string * string) list) (queue: string * string) =
    (contexts |> List.collect contextFindings) @ (boilerplateIn (fst queue) (snd queue))
