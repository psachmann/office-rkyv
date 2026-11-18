using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The sidebar's inbox and archive entries against the real app: where they are, which one is current, that they stay
/// in place while the visitor moves between the inbox and an archive, and where they are gone. Every test creates its
/// own user.
/// </summary>
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public class SidebarTests(AppHostFixture fixture) : PageTest
{
    private SignInPage SignIn => new(Page);

    private InboxPage Inbox => new(Page);

    private ArchivePage Archive => new(Page);

    private SidebarPage Sidebar => new(Page);

    private string BaseAddress =>
        fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri
        ?? throw new InvalidOperationException("The web service has no address.");

    [Test]
    public async Task TheEntries_AreInTheSidebarAboveSettings_AndThePagesHoldNoOtherList()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Gina {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Household");

        await Expect(Sidebar.InboxEntry).ToBeVisibleAsync();
        await Expect(Sidebar.InboxCount).ToHaveTextAsync("0");
        await Expect(Sidebar.ArchiveEntry("Household")).ToBeVisibleAsync();

        var body = Page.Locator(".rz-body");
        await Expect(body.GetByRole(AriaRole.Navigation)).ToHaveCountAsync(0);
        await Expect(body.GetByTestId("nav-inbox")).ToHaveCountAsync(0);
        await Expect(body.GetByTestId("nav-archive")).ToHaveCountAsync(0);

        var household = await Sidebar.ArchiveEntry("Household").BoundingBoxAsync();
        var settings = await new SettingsPage(Page).SidebarEntry("Settings").BoundingBoxAsync();
        await Assert.That(household!.Y + household.Height).IsLessThanOrEqualTo(settings!.Y);
    }

    [Test]
    public async Task TheCurrentEntry_IsTheOneOfThePageOpen()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Hank {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Taxes");

        await Expect(Sidebar.InboxEntry).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Sidebar.ArchiveEntry("Taxes")).Not.ToHaveAttributeAsync("aria-current", "page");

        await Sidebar.ArchiveEntry("Taxes").ClickAsync();
        await Expect(Archive.Heading).ToHaveTextAsync("Taxes");
        await Expect(Sidebar.ArchiveEntry("Taxes")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Sidebar.InboxEntry).Not.ToHaveAttributeAsync("aria-current", "page");
    }

    [Test]
    public async Task MovingBetweenTheInboxAndAnArchive_KeepsTheSidebarInPlace()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Ivy {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Household");
        var sidebar = await Sidebar.Sidebar.ElementHandleAsync() ?? throw new InvalidOperationException("No sidebar.");

        await Sidebar.ArchiveEntry("Household").ClickAsync();
        await Expect(Archive.Heading).ToHaveTextAsync("Household");
        await Sidebar.InboxEntry.ClickAsync();
        await Expect(Inbox.Heading).ToHaveTextAsync("Inbox");

        // The sidebar is the same element as before, so it was never rebuilt on the way, and the count was not
        // replaced by a loading state.
        await Assert.That(await sidebar.EvaluateAsync<bool>("element => element.isConnected")).IsTrue();
        await Expect(Sidebar.InboxCount).ToHaveTextAsync("0");
    }

    [Test]
    public async Task LeavingTheInboxAndArchives_ForSettingsOrBySigningOut_LeavesNoEntriesInTheSidebar()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Jay {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Household");
        await Expect(Sidebar.NavigationEntries).ToHaveCountAsync(2);

        var settings = new SettingsPage(Page);
        await settings.SidebarEntry("Settings").ClickAsync();
        await Expect(settings.Heading).ToBeVisibleAsync();
        await Expect(Sidebar.NavigationEntries).ToHaveCountAsync(0);

        await Page.GoBackAsync();
        await Expect(Sidebar.NavigationEntries).ToHaveCountAsync(2);

        // The landing page of a signed-in visitor is their inbox, so it is signing out that leaves it.
        await Sidebar.SignOutButton.ClickAsync();
        await Expect(new SignInPage(Page).LandingHeading).ToBeVisibleAsync();
        await Expect(Sidebar.NavigationEntries).ToHaveCountAsync(0);
    }

    [Test]
    public async Task CreatingAnArchive_KeepsTheOpenPage()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Kim {Guid.NewGuid():n}");

        await Sidebar.CreateArchiveAsync("Household");

        await Expect(Sidebar.ArchiveEntry("Household")).ToBeVisibleAsync();
        await Expect(Inbox.Heading).ToHaveTextAsync("Inbox");
        await Expect(Sidebar.InboxEntry).ToHaveAttributeAsync("aria-current", "page");
    }
}
