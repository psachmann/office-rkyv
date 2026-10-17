using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

public class BrowserParallelLimit : IParallelLimit
{
    public int Limit => 4;
}

/// <summary>
/// The landing page against the real app. These tests need the empty database first and then build on each
/// other's data, so they are ordered with <c>DependsOn</c> and have an AppHost of their own: the one the other
/// classes share gets users from every test that runs beside these.
/// </summary>
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerClass)]
public class LandingPageTests(AppHostFixture fixture) : PageTest
{
    private WorkspacePage Workspace => new(Page);

    private ILocator NewUserButton => Page.GetByRole(AriaRole.Button, new() { Name = "New user" });

    private ILocator CreateUserButton => Page.GetByRole(AriaRole.Button, new() { Name = "Create user" });

    /// <summary>The display names on the user cards, in the order the cards are shown.</summary>
    private ILocator CardNames => Page.GetByRole(AriaRole.Heading, new() { Level = 6 });

    private string BaseAddress =>
        fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri
        ?? throw new InvalidOperationException("The web service has no address.");

    [Before(Test)]
    public async Task Before() => await Page.GotoAsync(BaseAddress);

    [Test]
    public async Task EmptyState_IsShown_WhenNoUsersExist()
    {
        await Expect(Page.GetByText("No users yet")).ToBeVisibleAsync();
        await Expect(NewUserButton).ToBeVisibleAsync();
    }

    [Test]
    [DependsOn(nameof(EmptyState_IsShown_WhenNoUsersExist))]
    public async Task Dialog_HintsAtMissingFields_AndCreatesNothing()
    {
        await NewUserButton.ClickAsync();
        await CreateUserButton.ClickAsync();

        await Expect(Page.GetByText("Enter an email address.")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Enter a display name.")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Expect(Page.GetByText("No users yet")).ToBeVisibleAsync();
    }

    [Test]
    [DependsOn(nameof(Dialog_HintsAtMissingFields_AndCreatesNothing))]
    public async Task CreatingAUser_SignsInAsThem_AndOpensTheirEmptyWorkspace()
    {
        await NewUserButton.ClickAsync();
        await Page.GetByLabel("Email").FillAsync("Alice@Example.com");
        await Page.GetByLabel("Display name").FillAsync("  Alice  ");
        await CreateUserButton.ClickAsync();

        // The display name was trimmed by the server; the workspace is the new user's, with nothing in it.
        await Expect(Page).ToHaveURLAsync(new Regex("/workspace$"));
        await Expect(Workspace.Heading).ToHaveTextAsync("Alice");
        await Expect(Workspace.EmptyState).ToBeVisibleAsync();
        await Expect(Workspace.SignedInName).ToHaveTextAsync("Alice");
    }

    [Test]
    [DependsOn(nameof(CreatingAUser_SignsInAsThem_AndOpensTheirEmptyWorkspace))]
    public async Task EachUser_IsACard_WithTheEmailAsEntered()
    {
        await Expect(Workspace.UserCard("Alice")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Alice@Example.com", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("No users yet")).ToBeHiddenAsync();
    }

    [Test]
    [DependsOn(nameof(CreatingAUser_SignsInAsThem_AndOpensTheirEmptyWorkspace))]
    public async Task TheCards_AreOrderedByDisplayName_IgnoringCase()
    {
        // Created out of order, and one in lower case, so neither creation order nor ordinal order gives the result.
        await Workspace.CreateUserAsync("bob", "bob@example.com");
        await Expect(Workspace.Heading).ToHaveTextAsync("bob");
        await Workspace.SignOutButton.ClickAsync();
        await Workspace.CreateUserAsync("Carol", "carol@example.com");
        await Expect(Workspace.Heading).ToHaveTextAsync("Carol");
        await Workspace.SignOutButton.ClickAsync();

        await Expect(CardNames).ToHaveTextAsync(["Alice", "bob", "Carol"]);
    }

    [Test]
    [DependsOn(nameof(CreatingAUser_SignsInAsThem_AndOpensTheirEmptyWorkspace))]
    public async Task ChoosingTheCreateAction_WhileUsersExist_OpensTheSameForm()
    {
        await NewUserButton.ClickAsync();

        await Expect(Page.GetByLabel("Email")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Display name")).ToBeVisibleAsync();
    }

    [Test]
    [DependsOn(nameof(CreatingAUser_SignsInAsThem_AndOpensTheirEmptyWorkspace))]
    public async Task ADuplicateEmailInAnotherCase_ShowsTheLocalizedAlreadyInUseMessage_AndSignsNobodyIn()
    {
        await NewUserButton.ClickAsync();
        await Page.GetByLabel("Email").FillAsync("  ALICE@example.COM ");
        await Page.GetByLabel("Display name").FillAsync("Impostor");
        await CreateUserButton.ClickAsync();

        await Expect(Page.GetByText("A user with this email address already exists.")).ToBeVisibleAsync();

        // The form stays open with what was typed, ready to be corrected, and nobody is signed in.
        await Expect(Page.GetByLabel("Display name")).ToHaveValueAsync("Impostor");
        await Expect(Workspace.SignedInName).ToHaveCountAsync(0);
    }

    [Test]
    [DependsOn(nameof(CreatingAUser_SignsInAsThem_AndOpensTheirEmptyWorkspace))]
    public async Task InputTheClientDoesNotCheck_IsRejectedByTheServer_WithALocalizedMessage()
    {
        await NewUserButton.ClickAsync();
        await Page.GetByLabel("Email").FillAsync("dave@example.com");

        // There is no client-side length hint, so this only fails because the server enforces it.
        await Page.GetByLabel("Display name").FillAsync(new string('n', 101));
        await CreateUserButton.ClickAsync();

        await Expect(Page.GetByText("This display name is too long.")).ToBeVisibleAsync();
    }

    [Test]
    [DependsOn(nameof(CreatingAUser_SignsInAsThem_AndOpensTheirEmptyWorkspace))]
    public async Task Users_SurviveAReload()
    {
        await Page.ReloadAsync();

        await Expect(Workspace.UserCard("Alice")).ToBeVisibleAsync();
    }
}
