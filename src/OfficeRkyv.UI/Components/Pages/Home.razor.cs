using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using OfficeRkyv.Contracts.Users;
using OfficeRkyv.UI.Auth;
using OfficeRkyv.UI.Components.Pages.Users;
using Radzen;

namespace OfficeRkyv.UI.Components.Pages;

public partial class Home
{
    [CascadingParameter]
    private Task<AuthenticationState> AuthenticationState { get; set; } = null!;

    // Runs again whenever the authentication state changes, which is how a sign-in made in place (the desktop's)
    // reaches the workspace: the page that the visitor signed in on sends them on.
    protected override async Task OnParametersSetAsync()
    {
        // A signed-in visitor has no use for the landing page. The history entry is replaced, so back does not
        // return to a page that would send them on again.
        if (SignedInUser.IdOf((await AuthenticationState).User) is not null)
        {
            Navigation.NavigateTo("/workspace", replace: true);

            return;
        }

        Users.FindAllUsers.SetArgs(Unit.Default);
    }

    // The API lists users in the order they were created; a grid used to sort them, so the page does.
    private static IEnumerable<UserDto> InOrder(IReadOnlyList<UserDto> users) =>
        users.OrderBy(user => user.DisplayName, StringComparer.CurrentCultureIgnoreCase);

    // The first letter of the first two words of the display name.
    private static string Initials(string displayName) =>
        string.Concat(
            displayName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(word => char.ToUpperInvariant(word[0]))
        );

    private Task SignInAs(UserDto user) => SignIn.SignInAsync(Guid.Parse(user.Id));

    // The dialog closes with the user it created, who is then signed in.
    private async Task OpenCreateDialog()
    {
        var created = await DialogService.OpenAsync<CreateUserDialog>(
            Localizer["NewUser"],
            options: new DialogOptions { Width = "28rem" }
        );

        if (created is UserDto user)
        {
            await SignInAs(user);
        }
    }
}
