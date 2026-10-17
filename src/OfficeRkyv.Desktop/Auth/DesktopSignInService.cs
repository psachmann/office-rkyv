using Microsoft.AspNetCore.Components;
using OfficeRkyv.UI.Auth;

namespace OfficeRkyv.Desktop.Auth;

/// <summary>
/// The desktop's <see cref="ISignInService"/>, one per window: it changes the app-wide <see cref="DesktopSignIn"/> in
/// place, with no reload, and shows the page that follows in this window.
/// </summary>
/// <remarks>
/// A sign-in needs no navigation here: the landing page, where it is made, sends a signed-in visitor on to the
/// workspace as soon as the authentication state changes. A sign-out goes to the landing page itself, because it
/// can be made from a page that needs no user (the settings), which would otherwise stay open.
/// </remarks>
internal sealed class DesktopSignInService(DesktopSignIn signIn, NavigationManager navigation) : ISignInService
{
    public Task SignInAsync(Guid userId)
    {
        signIn.SignIn(userId);

        return Task.CompletedTask;
    }

    public Task SignOutAsync()
    {
        signIn.SignOut();
        navigation.NavigateTo("/", replace: true);

        return Task.CompletedTask;
    }
}
