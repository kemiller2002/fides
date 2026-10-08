namespace Fides

open System
open System.Security.Cryptography
open System.Text

/// PKCE (RFC 7636) with the S256 method only.
module Pkce =
    let private unreserved (c: char) =
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
        || c = '-' || c = '.' || c = '_' || c = '~'

    /// A verifier is 43-128 characters of `A-Z a-z 0-9 - . _ ~` (RFC 7636 4.1).
    let parseVerifier (value: string) : Result<CodeVerifier, unit> =
        if value.Length >= 43 && value.Length <= 128 && value |> Seq.forall unreserved then
            Ok(Secret.create value)
        else
            Error()

    /// Base64url without padding (RFC 7636 appendix A).
    let base64Url (bytes: byte array) =
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')

    /// The S256 challenge: BASE64URL(SHA256(ASCII(verifier))).
    let challenge (verifier: CodeVerifier) =
        Secret.reveal verifier |> Encoding.ASCII.GetBytes |> SHA256.HashData |> base64Url

    /// A verifier from 32 random bytes, which encodes to 43 characters.
    let verifierFrom (randomBytes: byte array) : Result<CodeVerifier, unit> =
        if randomBytes.Length = 32 then parseVerifier (base64Url randomBytes) else Error()
