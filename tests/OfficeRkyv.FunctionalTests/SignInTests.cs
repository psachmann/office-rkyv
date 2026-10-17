using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// Signing in and out against the real app. Every test creates its own users and has its own browser context, so
/// each has its own cookie and none looks at anybody else's data.
/// </summary>
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public class SignInTests(AppHostFixture fixture) : PageTest
{
    private WorkspacePage Workspace => new(Page);

    private string BaseAddress =>
        fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri
        ?? throw new InvalidOperationException("The web service has no address.");

    private static string NewName(string prefix) => $"{prefix} {Guid.NewGuid():n}";

    [Test]
    public async Task ChoosingAUsersCard_SignsInAsThatUser()
    {
        var name = NewName("Alice");
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, name);
        await Workspace.SignOutButton.ClickAsync();
        await Expect(Workspace.LandingHeading).ToBeVisibleAsync();

        await Workspace.UserCard(name).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/workspace$"));
        await Expect(Workspace.Heading).ToHaveTextAsync(name);
        await Expect(Workspace.InboxEntry).ToHaveAttributeAsync("aria-current", "page");
    }

    [Test]
    public async Task AReload_KeepsTheUserSignedIn()
    {
        var name = NewName("Bob");
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, name);

        await Page.ReloadAsync();

        await Expect(Workspace.Heading).ToHaveTextAsync(name);
    }

    [Test]
    public async Task ANewTabOfTheSameBrowser_IsSignedInToo()
    {
        var name = NewName("Carol");
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, name);

        var other = await Context.NewPageAsync();
        await other.GotoAsync($"{BaseAddress}workspace");

        await Expect(new WorkspacePage(other).Heading).ToHaveTextAsync(name);
    }

    [Test]
    public async Task SigningOut_ShowsTheLandingPage_AndTheWorkspaceAddressThenShowsItAgain()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, NewName("Dave"));

        await Workspace.SignOutButton.ClickAsync();

        await Expect(Workspace.LandingHeading).ToBeVisibleAsync();
        await Page.GotoAsync($"{BaseAddress}workspace");
        await Expect(Workspace.LandingHeading).ToBeVisibleAsync();
        await Expect(Workspace.Heading).ToHaveCountAsync(0);
    }

    [Test]
    public async Task TheSidebar_ShowsNoUserAndNoSignOut_WhileNobodyIsSignedIn()
    {
        await Page.GotoAsync(BaseAddress);

        await Expect(Workspace.LandingHeading).ToBeVisibleAsync();
        await Expect(Workspace.SignedInName).ToHaveCountAsync(0);
        await Expect(Workspace.SignOutButton).ToHaveCountAsync(0);
    }

    [Test]
    public async Task AWorkspaceAddress_WhileSignedOut_ShowsTheLandingPage()
    {
        await Page.GotoAsync($"{BaseAddress}workspace");

        await Expect(Workspace.LandingHeading).ToBeVisibleAsync();
        await Expect(Workspace.Heading).ToHaveCountAsync(0);

        await Page.GotoAsync($"{BaseAddress}workspace/archives/{Guid.NewGuid()}");

        await Expect(Workspace.LandingHeading).ToBeVisibleAsync();
    }

    [Test]
    public async Task TheLandingPage_WhileSignedIn_ShowsTheWorkspace()
    {
        var name = NewName("Erin");
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, name);

        await Page.GotoAsync(BaseAddress);

        await Expect(Page).ToHaveURLAsync(new Regex("/workspace$"));
        await Expect(Workspace.Heading).ToHaveTextAsync(name);
    }

    [Test]
    public async Task Settings_IsAvailable_WhileSignedOut()
    {
        var settings = new SettingsPage(Page);

        await settings.OpenAsync(BaseAddress);

        await Expect(settings.Heading).ToBeVisibleAsync();
        await Expect(Workspace.SignedInName).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ASignInAddressWithAMadeUpTicket_SignsNobodyIn()
    {
        await Page.GotoAsync($"{BaseAddress}account/sign-in?ticket={Guid.NewGuid():n}");

        await Expect(Workspace.LandingHeading).ToBeVisibleAsync();
        await Expect(Workspace.SignedInName).ToHaveCountAsync(0);
        await Page.GotoAsync($"{BaseAddress}workspace");
        await Expect(Workspace.LandingHeading).ToBeVisibleAsync();
    }

    [Test]
    public async Task ASignInAddressUsedAgain_SignsNobodyIn()
    {
        var name = NewName("Frank");
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, name);
        await Workspace.SignOutButton.ClickAsync();
        await Expect(Workspace.LandingHeading).ToBeVisibleAsync();

        // The ticket of the sign-in that this card click issues is in the address the browser is sent to.
        var signInAddress = new TaskCompletionSource<string>();
        Page.Request += (_, request) =>
        {
            if (request.Url.Contains("/account/sign-in?ticket="))
            {
                signInAddress.TrySetResult(request.Url);
            }
        };
        await Workspace.UserCard(name).ClickAsync();
        await Expect(Workspace.Heading).ToBeVisibleAsync();
        var used = await signInAddress.Task;

        // A second browser opens the same address: the ticket worked once, in the first one.
        var other = await Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var otherPage = await other.NewPageAsync();
        await otherPage.GotoAsync(used);

        await Expect(new WorkspacePage(otherPage).LandingHeading).ToBeVisibleAsync();
        await Expect(new WorkspacePage(otherPage).SignedInName).ToHaveCountAsync(0);
        await other.CloseAsync();
    }

    [Test]
    public async Task TwoBrowsers_SignedInAsTwoUsers_EachSeeOnlyTheirOwnDocuments()
    {
        var alice = NewName("Alice");
        var bob = NewName("Bob");
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, alice);
        await Workspace.CreateDocumentAsync("Alices invoice");

        var otherContext = await Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var otherPage = await otherContext.NewPageAsync();
        var bobsWorkspace = new WorkspacePage(otherPage);
        await bobsWorkspace.OpenNewUsersWorkspaceAsync(BaseAddress, bob);
        await bobsWorkspace.CreateDocumentAsync("Bobs lease");

        await Expect(Workspace.DocumentRow("Alices invoice")).ToBeVisibleAsync();
        await Expect(Workspace.DocumentRow("Bobs lease")).ToHaveCountAsync(0);
        await Expect(bobsWorkspace.DocumentRow("Bobs lease")).ToBeVisibleAsync();
        await Expect(bobsWorkspace.DocumentRow("Alices invoice")).ToHaveCountAsync(0);

        // Opening one's own address as the other user does not show anything of the first: the address holds no user.
        await otherPage.GotoAsync(Page.Url);
        await Expect(bobsWorkspace.Heading).ToHaveTextAsync(bob);
        await Expect(bobsWorkspace.DocumentRow("Alices invoice")).ToHaveCountAsync(0);
        await otherContext.CloseAsync();
    }

    [Test]
    public async Task AnArchiveOfAnotherUser_InTheAddress_ShowsTheInbox()
    {
        var alice = NewName("Alice");
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, alice);
        await Workspace.CreateArchiveAsync("Taxes");
        await Workspace.ArchiveEntry("Taxes").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/workspace/archives/[0-9a-f-]{36}$"));
        var alicesArchiveAddress = Page.Url;

        var otherContext = await Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var otherPage = await otherContext.NewPageAsync();
        var bobsWorkspace = new WorkspacePage(otherPage);
        await bobsWorkspace.OpenNewUsersWorkspaceAsync(BaseAddress, NewName("Bob"));
        await otherPage.GotoAsync(alicesArchiveAddress);

        await Expect(bobsWorkspace.Selection).ToHaveTextAsync("Inbox");
        await Expect(bobsWorkspace.ArchiveEntries).ToHaveCountAsync(0);
        await otherContext.CloseAsync();
    }

    [Test]
    public async Task TheOldUserAddress_ShowsNotFound_AndNoWorkspace()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, NewName("Gina"));

        await Page.GotoAsync($"{BaseAddress}users/{Guid.NewGuid()}");

        await Expect(Workspace.NotFound).ToBeVisibleAsync();
        await Expect(Workspace.Heading).ToHaveCountAsync(0);
    }
}
