using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The landing page as a person uses it: the users to sign in as, creating a user, and what a signed-out visitor is
/// shown. A user's card signs the visitor in; a created user is signed in and sent to the inbox.
/// </summary>
public sealed class SignInPage(IPage page)
{
    public ILocator Dialog => page.GetByRole(AriaRole.Dialog);

    /// <summary>The landing page's heading: what a visitor who is signed out is shown.</summary>
    public ILocator LandingHeading => page.GetByRole(AriaRole.Heading, new() { Name = "Users", Exact = true });

    /// <summary>The page every address the app does not know shows.</summary>
    public ILocator NotFound => page.GetByRole(AriaRole.Heading, new() { Name = "Not Found", Exact = true });

    /// <summary>A user's card on the landing page: a button named by the display name.</summary>
    public ILocator UserCard(string displayName) =>
        page.GetByRole(AriaRole.Button, new() { Name = displayName, Exact = true });

    /// <summary>
    /// Creates a user on the landing page, which signs the visitor in as them and opens their inbox. Each browser
    /// context has its own sign-in, so tests that do this stay independent.
    /// </summary>
    public async Task OpenNewUsersInboxAsync(string baseAddress, string displayName)
    {
        await page.GotoAsync(baseAddress);
        await CreateUserAsync(displayName);

        await Expect(page).ToHaveURLAsync(new Regex("/inbox$"));
        await Expect(new SidebarPage(page).SignedInName).ToHaveTextAsync(displayName);
        await Expect(new InboxPage(page).Heading).ToHaveTextAsync("Inbox");
    }

    /// <summary>Fills the create-user form of the landing page with a fresh email and submits it.</summary>
    public async Task CreateUserAsync(string displayName, string? email = null)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "New user" }).First.ClickAsync();
        await page.GetByLabel("Email").FillAsync(email ?? $"{Guid.NewGuid():n}@example.com");
        await page.GetByLabel("Display name").FillAsync(displayName);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create user" }).ClickAsync();
    }
}
