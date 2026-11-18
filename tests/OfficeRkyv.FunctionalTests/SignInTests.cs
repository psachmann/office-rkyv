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
    private SignInPage SignIn => new(Page);

    private InboxPage Inbox => new(Page);

    private ArchivePage Archive => new(Page);

    private SidebarPage Sidebar => new(Page);

    private DocumentListPage Documents => new(Page);

    private string BaseAddress =>
        fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri
        ?? throw new InvalidOperationException("The web service has no address.");

    private static string NewName(string prefix) => $"{prefix} {Guid.NewGuid():n}";

    [Test]
    public async Task ChoosingAUsersCard_SignsInAsThatUser()
    {
        var name = NewName("Alice");
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, name);
        await Sidebar.SignOutButton.ClickAsync();
        await Expect(SignIn.LandingHeading).ToBeVisibleAsync();

        await SignIn.UserCard(name).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/inbox$"));
        await Expect(Sidebar.SignedInName).ToHaveTextAsync(name);
        await Expect(Sidebar.InboxEntry).ToHaveAttributeAsync("aria-current", "page");
    }

    [Test]
    public async Task AReload_KeepsTheUserSignedIn()
    {
        var name = NewName("Bob");
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, name);

        await Page.ReloadAsync();

        await Expect(Sidebar.SignedInName).ToHaveTextAsync(name);
    }

    [Test]
    public async Task ANewTabOfTheSameBrowser_IsSignedInToo()
    {
        var name = NewName("Carol");
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, name);

        var other = await Context.NewPageAsync();
        await other.GotoAsync($"{BaseAddress}inbox");

        await Expect(new SidebarPage(other).SignedInName).ToHaveTextAsync(name);
    }

    [Test]
    public async Task SigningOut_ShowsTheLandingPage_AndTheInboxAddressThenShowsItAgain()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, NewName("Dave"));

        await Sidebar.SignOutButton.ClickAsync();

        await Expect(SignIn.LandingHeading).ToBeVisibleAsync();
        await Page.GotoAsync($"{BaseAddress}inbox");
        await Expect(SignIn.LandingHeading).ToBeVisibleAsync();
        await Expect(Documents.Heading).ToHaveCountAsync(0);
    }

    [Test]
    public async Task TheSidebar_ShowsNoUserAndNoSignOut_WhileNobodyIsSignedIn()
    {
        await Page.GotoAsync(BaseAddress);

        await Expect(SignIn.LandingHeading).ToBeVisibleAsync();
        await Expect(Sidebar.SignedInName).ToHaveCountAsync(0);
        await Expect(Sidebar.SignOutButton).ToHaveCountAsync(0);
    }

    [Test]
    public async Task AnArchiveAddress_WhileSignedOut_ShowsTheLandingPage()
    {
        await Page.GotoAsync($"{BaseAddress}archives/{Guid.NewGuid()}");

        await Expect(SignIn.LandingHeading).ToBeVisibleAsync();
        await Expect(Documents.Heading).ToHaveCountAsync(0);
    }

    [Test]
    public async Task AnInboxAddress_WhileSignedOut_ShowsTheLandingPage()
    {
        await Page.GotoAsync($"{BaseAddress}inbox");

        await Expect(SignIn.LandingHeading).ToBeVisibleAsync();
        await Expect(Documents.Heading).ToHaveCountAsync(0);

        await Page.GotoAsync($"{BaseAddress}archives/{Guid.NewGuid()}");

        await Expect(SignIn.LandingHeading).ToBeVisibleAsync();
    }

    [Test]
    public async Task TheLandingPage_WhileSignedIn_ShowsTheInbox()
    {
        var name = NewName("Erin");
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, name);

        await Page.GotoAsync(BaseAddress);

        await Expect(Page).ToHaveURLAsync(new Regex("/inbox$"));
        await Expect(Sidebar.SignedInName).ToHaveTextAsync(name);
    }

    [Test]
    public async Task Settings_IsAvailable_WhileSignedOut()
    {
        var settings = new SettingsPage(Page);

        await settings.OpenAsync(BaseAddress);

        await Expect(settings.Heading).ToBeVisibleAsync();
        await Expect(Sidebar.SignedInName).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ASignInAddressWithAMadeUpTicket_SignsNobodyIn()
    {
        await Page.GotoAsync($"{BaseAddress}account/sign-in?ticket={Guid.NewGuid():n}");

        await Expect(SignIn.LandingHeading).ToBeVisibleAsync();
        await Expect(Sidebar.SignedInName).ToHaveCountAsync(0);
        await Page.GotoAsync($"{BaseAddress}inbox");
        await Expect(SignIn.LandingHeading).ToBeVisibleAsync();
    }

    [Test]
    public async Task ASignInAddressUsedAgain_SignsNobodyIn()
    {
        var name = NewName("Frank");
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, name);
        await Sidebar.SignOutButton.ClickAsync();
        await Expect(SignIn.LandingHeading).ToBeVisibleAsync();

        // The ticket of the sign-in that this card click issues is in the address the browser is sent to.
        var signInAddress = new TaskCompletionSource<string>();
        Page.Request += (_, request) =>
        {
            if (request.Url.Contains("/account/sign-in?ticket="))
            {
                signInAddress.TrySetResult(request.Url);
            }
        };
        await SignIn.UserCard(name).ClickAsync();
        await Expect(Documents.Heading).ToBeVisibleAsync();
        var used = await signInAddress.Task;

        // A second browser opens the same address: the ticket worked once, in the first one.
        var other = await Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var otherPage = await other.NewPageAsync();
        await otherPage.GotoAsync(used);

        await Expect(new SignInPage(otherPage).LandingHeading).ToBeVisibleAsync();
        await Expect(new SidebarPage(otherPage).SignedInName).ToHaveCountAsync(0);
        await other.CloseAsync();
    }

    [Test]
    public async Task TwoBrowsers_SignedInAsTwoUsers_EachSeeOnlyTheirOwnDocuments()
    {
        var alice = NewName("Alice");
        var bob = NewName("Bob");
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, alice);
        await Inbox.CreateDocumentAsync("Alices invoice");

        var otherContext = await Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var otherPage = await otherContext.NewPageAsync();
        var bobsSignIn = new SignInPage(otherPage);
        await bobsSignIn.OpenNewUsersInboxAsync(BaseAddress, bob);
        var bobsInbox = new InboxPage(otherPage);
        await bobsInbox.CreateDocumentAsync("Bobs lease");

        await Expect(Inbox.DocumentRow("Alices invoice")).ToBeVisibleAsync();
        await Expect(Inbox.DocumentRow("Bobs lease")).ToHaveCountAsync(0);
        await Expect(bobsInbox.DocumentRow("Bobs lease")).ToBeVisibleAsync();
        await Expect(bobsInbox.DocumentRow("Alices invoice")).ToHaveCountAsync(0);

        // Opening one's own address as the other user does not show anything of the first: the address holds no user.
        await otherPage.GotoAsync(Page.Url);
        await Expect(new SidebarPage(otherPage).SignedInName).ToHaveTextAsync(bob);
        await Expect(bobsInbox.DocumentRow("Alices invoice")).ToHaveCountAsync(0);
        await otherContext.CloseAsync();
    }

    [Test]
    public async Task TheOldUserAddress_ShowsNotFound()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, NewName("Gina"));

        await Page.GotoAsync($"{BaseAddress}users/{Guid.NewGuid()}");

        await Expect(SignIn.NotFound).ToBeVisibleAsync();
        await Expect(Documents.Heading).ToHaveCountAsync(0);
    }
}
