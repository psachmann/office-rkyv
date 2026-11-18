using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// An archive's page against the real app: its documents, renaming and deleting it, and what an address that does
/// not name one of the user's archives shows. Every test creates its own user.
/// </summary>
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public class ArchiveTests(AppHostFixture fixture) : PageTest
{
    private SignInPage SignIn => new(Page);

    private InboxPage Inbox => new(Page);

    private ArchivePage Archive => new(Page);

    private SidebarPage Sidebar => new(Page);

    private string BaseAddress =>
        fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri
        ?? throw new InvalidOperationException("The web service has no address.");

    [Test]
    public async Task CreatingAnArchiveAndADocument_FilingIt_AndDeletingTheArchive_ReturnsTheDocumentToTheInbox()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Alice {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Household");
        await Inbox.CreateDocumentAsync("Invoice March");
        await Inbox.FileDocumentIntoAsync("Invoice March", "Household");
        await Expect(Inbox.DocumentRow("Invoice March")).ToBeHiddenAsync();

        // Filing changed the archive, and the list of archives is loaded again after that. A reload loads it once more
        // before the delete, so the delete is made with the archive's current version and not the one shown before.
        await Page.ReloadAsync();
        await Expect(Sidebar.InboxEntry).ToBeVisibleAsync();
        await Sidebar.ArchiveEntry("Household").ClickAsync();
        await Expect(Archive.Heading).ToHaveTextAsync("Household");
        await Expect(Archive.DocumentRow("Invoice March")).ToBeVisibleAsync();

        // Deleting the archive says where its documents go, opens the inbox, and the document comes back.
        await Archive.DeleteArchiveButton("Household").ClickAsync();
        await Expect(Archive.ConfirmationDialog).ToContainTextAsync("return to the inbox");
        await Archive
            .ConfirmationDialog.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true })
            .ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/inbox$"));
        await Expect(Inbox.Heading).ToHaveTextAsync("Inbox");
        await Expect(Sidebar.ArchiveEntry("Household")).ToBeHiddenAsync();
        await Expect(Inbox.DocumentRow("Invoice March")).ToBeVisibleAsync();
        await Expect(Sidebar.InboxCount).ToHaveTextAsync("1");
    }

    [Test]
    public async Task RenamingAnArchive_AndADocumentInTheInbox_UpdatesTheListsWithoutAReload()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Carol {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Taxes");
        await Inbox.CreateDocumentAsync("Invoice March");

        await Sidebar.ArchiveEntry("Taxes").ClickAsync();
        await Archive.RenameArchiveAsync("Taxes", "Taxes 2026");
        await Expect(Archive.Heading).ToHaveTextAsync("Taxes 2026");
        await Expect(Sidebar.ArchiveEntry("Taxes 2026")).ToHaveAttributeAsync("aria-current", "page");

        await Sidebar.InboxEntry.ClickAsync();
        await Inbox.RenameDocumentAsync("Invoice March", "Invoice April");
        await Expect(Inbox.DocumentRow("Invoice April")).ToBeVisibleAsync();
        await Expect(Inbox.DocumentRow("Invoice March")).ToBeHiddenAsync();
    }

    [Test]
    public async Task ArchiveActions_AreOfferedOnlyOnTheArchivesPage_AndNoNewDocumentIsOffered()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Iris {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Taxes");

        await Expect(Inbox.Heading).ToHaveTextAsync("Inbox");
        await Expect(Archive.RenameArchiveButton("Taxes")).ToHaveCountAsync(0);
        await Expect(Archive.DeleteArchiveButton("Taxes")).ToHaveCountAsync(0);

        await Sidebar.ArchiveEntry("Taxes").ClickAsync();
        await Expect(Archive.RenameArchiveButton("Taxes")).ToBeVisibleAsync();
        await Expect(Archive.DeleteArchiveButton("Taxes")).ToBeVisibleAsync();
        await Expect(Archive.NewDocumentButton).ToHaveCountAsync(0);

        await Sidebar.InboxEntry.ClickAsync();
        await Expect(Inbox.Heading).ToHaveTextAsync("Inbox");
        await Expect(Archive.RenameArchiveButton("Taxes")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task AnUnknownArchiveId_ShowsNotFound()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Kai {Guid.NewGuid():n}");

        await Page.GotoAsync($"{BaseAddress}archives/{Guid.NewGuid()}");

        await Expect(SignIn.NotFound).ToBeVisibleAsync();
        await Expect(Sidebar.NavigationEntries).ToHaveCountAsync(0);
    }

    [Test]
    public async Task AnAddressThatIsNotAnArchiveId_ShowsNotFound()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Lena {Guid.NewGuid():n}");

        await Page.GotoAsync($"{BaseAddress}archives/not-a-guid");

        await Expect(SignIn.NotFound).ToBeVisibleAsync();
    }

    [Test]
    public async Task AnArchiveOfAnotherUser_InTheAddress_ShowsNotFound_AndNoneOfTheirDocuments()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Mira {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Mira's taxes");
        await Sidebar.ArchiveEntry("Mira's taxes").ClickAsync();
        await Expect(Archive.Heading).ToHaveTextAsync("Mira's taxes");
        var address = Page.Url;

        // A second browser is a second user: it may not see the first one's archive by its address.
        var other = await (await Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true })).NewPageAsync();
        var otherSignIn = new SignInPage(other);
        await otherSignIn.OpenNewUsersInboxAsync(BaseAddress, $"Nora {Guid.NewGuid():n}");
        await other.GotoAsync(address);

        await Expect(otherSignIn.NotFound).ToBeVisibleAsync();
        await Expect(new SidebarPage(other).NavigationEntries).ToHaveCountAsync(0);
    }

    [Test]
    public async Task AnArchiveDeletedElsewhere_WhileItsPageIsOpen_LeadsToTheInbox()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Otto {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Household");
        await Sidebar.ArchiveEntry("Household").ClickAsync();
        await Expect(Archive.Heading).ToHaveTextAsync("Household");
        var address = Page.Url;

        // Another tab of the same browser deletes it while this one still shows it.
        var other = await Context.NewPageAsync();
        await other.GotoAsync(address);
        await new ArchivePage(other).DeleteArchiveAsync("Household");
        await Expect(new SidebarPage(other).ArchiveEntry("Household")).ToBeHiddenAsync();

        // This tab's list is refreshed by anything that changes it; creating an archive does.
        await Sidebar.CreateArchiveAsync("Other");

        await Expect(Page).ToHaveURLAsync(new Regex("/inbox$"));
        await Expect(Inbox.Heading).ToHaveTextAsync("Inbox");
        await Expect(Sidebar.ArchiveEntry("Household")).ToBeHiddenAsync();
    }
}
