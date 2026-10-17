using System.Security.Claims;

namespace OfficeRkyv.UI.Auth;

/// <summary>
/// The signed-in user as the UI sees them: a principal whose <see cref="ClaimTypes.NameIdentifier"/> is the user's
/// id. Both hosts, and a later passkey sign-in, produce exactly this principal.
/// </summary>
public static class SignedInUser
{
    /// <summary>The authentication type of the principals <see cref="PrincipalFor"/> makes.</summary>
    public const string AuthenticationType = "office-rkyv";

    /// <summary>The id of the signed-in user, or null for a visitor who is signed out.</summary>
    public static Guid? IdOf(ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true
        && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public static ClaimsPrincipal PrincipalFor(Guid userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], AuthenticationType));

    /// <summary>What a visitor who is signed out is.</summary>
    public static ClaimsPrincipal Anonymous => new(new ClaimsIdentity());
}
