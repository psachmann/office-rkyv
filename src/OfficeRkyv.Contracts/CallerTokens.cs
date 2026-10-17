namespace OfficeRkyv.Contracts;

/// <summary>
/// What the UI host that issues a caller token and the API that validates it must agree on. The token is a JWT
/// signed with HMAC-SHA256 whose <c>sub</c> is the id of the user making the call; it travels as
/// <c>Authorization: Bearer</c> on every call.
/// </summary>
public static class CallerTokens
{
    /// <summary>Who issues the token: the UI host, in either host process.</summary>
    public const string Issuer = "office-rkyv-ui";

    /// <summary>Who the token is for: the API.</summary>
    public const string Audience = "office-rkyv-api";

    /// <summary>The configuration setting holding the signing key, in the form a configuration file uses.</summary>
    public const string SigningKeySetting = "CallerTokens:SigningKey";

    /// <summary>The shortest key accepted, in bytes: the size of an HMAC-SHA256 key.</summary>
    public const int MinimumKeyLength = 32;

    /// <summary>
    /// How long a token is valid. It is issued per call, so a short life costs nothing and limits what a leaked
    /// token is worth.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
}
