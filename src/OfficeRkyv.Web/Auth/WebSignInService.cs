using Microsoft.AspNetCore.Components;
using OfficeRkyv.UI.Auth;

namespace OfficeRkyv.Web.Auth;

/// <summary>
/// The web host's sign-in, one per circuit. A circuit cannot set a cookie, so signing in or out asks
/// <see cref="SignInTickets"/> for a ticket and navigates, with a full page load, to the endpoint that redeems it
/// (see <see cref="AccountEndpoints"/>). The full load also gives the visitor a fresh circuit that starts with
/// the new sign-in state, which a live circuit could not change.
/// </summary>
public sealed class WebSignInService(SignInTickets tickets, NavigationManager navigation) : ISignInService
{
    public Task SignInAsync(Guid userId)
    {
        navigation.NavigateTo(AccountEndpoints.SignInPath(tickets.IssueSignIn(userId)), forceLoad: true);

        return Task.CompletedTask;
    }

    public Task SignOutAsync()
    {
        navigation.NavigateTo(AccountEndpoints.SignOutPath(tickets.IssueSignOut()), forceLoad: true);

        return Task.CompletedTask;
    }
}
