using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

public class BrowserParallelLimit : IParallelLimit
{
    public int Limit => 4;
}

/// <summary>
/// One AppHost (and so one empty SQLite database) is shared by the whole session, so these tests
/// build on each other's data and are ordered with <c>DependsOn</c>: the empty state has to come
/// first, and everything else needs the user the create test adds.
/// </summary>
[Skip("Currently this test is a show case and the expects needed to be switched to TUnit assertions")]
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AspireFixture<Projects.OfficeRkyv_AppHost>>(Shared = SharedType.PerTestSession)]
public class LandingPageTests(AspireFixture<Projects.OfficeRkyv_AppHost> fixture) : PageTest
{
    private ILocator NewUserButton => Page.GetByRole(AriaRole.Button, new() { Name = "New user" });
    private ILocator CreateUserButton => Page.GetByRole(AriaRole.Button, new() { Name = "Create user" });

    [Before(Test)]
    public async Task Before()
    {
        var address = fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri;

        Assert.NotNull(address);

        await Page.GotoAsync(address);
    }

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
    public async Task CreatingAUser_ReplacesTheEmptyStateWithTheList_WithoutAReload()
    {
        await NewUserButton.ClickAsync();
        await Page.GetByLabel("Email").FillAsync("Alice@Example.com");
        await Page.GetByLabel("Display name").FillAsync("  Alice  ");
        await CreateUserButton.ClickAsync();

        // The email is shown as entered; the display name was trimmed by the server.
        await Expect(Page.GetByText("Alice@Example.com", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Alice", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("No users yet")).ToBeHiddenAsync();
    }

    [Test]
    [DependsOn(nameof(CreatingAUser_ReplacesTheEmptyStateWithTheList_WithoutAReload))]
    public async Task ExistingUsers_AreListed_AndAnotherCanStillBeCreated()
    {
        await Expect(Page.GetByText("Alice@Example.com", new() { Exact = true })).ToBeVisibleAsync();

        await NewUserButton.ClickAsync();
        await Page.GetByLabel("Email").FillAsync("bob@example.com");
        await Page.GetByLabel("Display name").FillAsync("Bob");
        await CreateUserButton.ClickAsync();

        await Expect(Page.GetByText("bob@example.com", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Alice@Example.com", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    [DependsOn(nameof(CreatingAUser_ReplacesTheEmptyStateWithTheList_WithoutAReload))]
    public async Task ADuplicateEmailInAnotherCase_ShowsTheLocalizedAlreadyInUseMessage()
    {
        await NewUserButton.ClickAsync();
        await Page.GetByLabel("Email").FillAsync("  ALICE@example.COM ");
        await Page.GetByLabel("Display name").FillAsync("Impostor");
        await CreateUserButton.ClickAsync();

        await Expect(Page.GetByText("A user with this email address already exists.")).ToBeVisibleAsync();

        // The form stays open with what was typed, ready to be corrected.
        await Expect(Page.GetByLabel("Display name")).ToHaveValueAsync("Impostor");
    }

    [Test]
    [DependsOn(nameof(CreatingAUser_ReplacesTheEmptyStateWithTheList_WithoutAReload))]
    public async Task InputTheClientDoesNotCheck_IsRejectedByTheServer_WithALocalizedMessage()
    {
        await NewUserButton.ClickAsync();
        await Page.GetByLabel("Email").FillAsync("carol@example.com");

        // There is no client-side length hint, so this only fails because the server enforces it.
        await Page.GetByLabel("Display name").FillAsync(new string('n', 101));
        await CreateUserButton.ClickAsync();

        await Expect(Page.GetByText("This display name is too long.")).ToBeVisibleAsync();
    }

    [Test]
    [DependsOn(nameof(CreatingAUser_ReplacesTheEmptyStateWithTheList_WithoutAReload))]
    public async Task Users_SurviveAReload()
    {
        await Page.ReloadAsync();

        await Expect(Page.GetByText("Alice@Example.com", new() { Exact = true })).ToBeVisibleAsync();
    }
}
