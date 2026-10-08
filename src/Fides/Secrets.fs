namespace Fides

/// Kinds of secret material. They exist only to keep secrets of different
/// kinds from being mixed up; nothing is ever an instance of them.
module SecretKind =
    [<AbstractClass; Sealed>]
    type AccessToken = class end

    [<AbstractClass; Sealed>]
    type RefreshToken = class end

    [<AbstractClass; Sealed>]
    type AuthorizationCode = class end

    [<AbstractClass; Sealed>]
    type CodeVerifier = class end

    [<AbstractClass; Sealed>]
    type ClientSecret = class end

/// Secret material: a token, code, verifier or client secret. It prints,
/// formats and interpolates as `<redacted>`, so a secret that reaches a log
/// line, an exception message or `%A` output by accident discloses nothing
/// (FID-EXC-005). The value is read only through `Secret.reveal`, at the
/// single point where it must cross a boundary.
[<Sealed; StructuredFormatDisplay("<redacted>")>]
type Secret<'kind>(value: string) =
    member internal _.Value = value
    override _.ToString() = "<redacted>"

    override this.Equals other =
        match other with
        | :? Secret<'kind> as secret -> secret.Value = this.Value
        | _ -> false

    override _.GetHashCode() = value.GetHashCode()

/// A GitHub (or other provider) user access token.
type AccessToken = Secret<SecretKind.AccessToken>

/// A provider refresh token.
type RefreshToken = Secret<SecretKind.RefreshToken>

/// An OAuth authorization code.
type AuthorizationCode = Secret<SecretKind.AuthorizationCode>

/// A PKCE code verifier (RFC 7636).
type CodeVerifier = Secret<SecretKind.CodeVerifier>

/// The OAuth client secret. It exists only in the exchange.
type ClientSecret = Secret<SecretKind.ClientSecret>

module Secret =
    /// Wraps secret material.
    let create<'kind> (value: string) : Secret<'kind> = Secret<'kind> value

    /// The secret's value. Call it only where the value must leave Fides: a
    /// provider request, the exchange's response, or the client's store.
    let reveal (secret: Secret<'kind>) = secret.Value
