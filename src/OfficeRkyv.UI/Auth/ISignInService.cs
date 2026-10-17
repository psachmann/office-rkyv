namespace OfficeRkyv.UI.Auth;

/// <summary>
/// How a visitor becomes the signed-in user and stops being one. The UI reads who is signed in only through
/// Blazor's <c>AuthenticationStateProvider</c> (and <see cref="SignedInUser"/>); it acts through this, because
/// the hosts differ in how a sign-in is made: the web host sets a cookie, which needs a real navigation, and the
/// desktop keeps the user in memory and changes in place.
/// </summary>
public interface ISignInService
{
    /// <summary>Signs in as the user, and shows that user's workspace.</summary>
    Task SignInAsync(Guid userId);

    /// <summary>Ends the sign-in, and shows the landing page.</summary>
    Task SignOutAsync();
}
