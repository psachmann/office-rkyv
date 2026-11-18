using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// What a person does with a document on the inbox and on an archive page: filing, renaming, deleting, downloading, and
/// what a refusal says. Every test creates its own user.
/// </summary>
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public class DocumentActionsTests(AppHostFixture fixture) : PageTest
{
    private SignInPage SignIn => new(Page);

    private InboxPage Inbox => new(Page);

    private ArchivePage Archive => new(Page);

    private SidebarPage Sidebar => new(Page);

    private string BaseAddress =>
        fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri
        ?? throw new InvalidOperationException("The web service has no address.");

    [Test]
    public async Task UnfilingADocument_ReturnsItToTheInbox_AndDeletingItRemovesItEverywhere()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Dave {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Household");
        await Inbox.CreateDocumentAsync("Lease");
        await Inbox.FileDocumentIntoAsync("Lease", "Household");
        await Sidebar.ArchiveEntry("Household").ClickAsync();
        await Expect(Archive.DocumentRow("Lease")).ToBeVisibleAsync();

        // From inside the archive, the choice offers the inbox (and no other archive exists here).
        await Archive.FileDocumentIntoAsync("Lease", "Inbox");
        await Expect(Archive.DocumentRow("Lease")).ToBeHiddenAsync();
        await Expect(Sidebar.InboxCount).ToHaveTextAsync("1");

        await Sidebar.InboxEntry.ClickAsync();
        await Inbox.DeleteDocumentAsync("Lease");
        await Expect(Inbox.DocumentRow("Lease")).ToBeHiddenAsync();
        await Expect(Sidebar.InboxCount).ToHaveTextAsync("0");
        await Expect(Inbox.EmptyState).ToBeVisibleAsync();
    }

    [Test]
    public async Task FilingIntoAnArchiveDeletedElsewhere_ExplainsWhy_AndTheArchiveDisappears()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Erin {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Household");
        await Inbox.CreateDocumentAsync("Invoice");
        var archiveAddress = await OpenArchiveAsync("Household");
        await Sidebar.InboxEntry.ClickAsync();

        // Another tab deletes the archive while this one still lists it.
        var other = await Context.NewPageAsync();
        await other.GotoAsync(archiveAddress);
        await new ArchivePage(other).DeleteArchiveAsync("Household");
        await Expect(new SidebarPage(other).ArchiveEntry("Household")).ToBeHiddenAsync();

        await Inbox.FileDocumentIntoAsync("Invoice", "Household");

        await Expect(Inbox.Error).ToContainTextAsync("This archive has been deleted.");
        await Expect(Sidebar.ArchiveEntry("Household")).ToBeHiddenAsync();
        await Expect(Inbox.DocumentRow("Invoice")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ARenameOfADocumentChangedElsewhere_ExplainsWhy_AndShowsTheCurrentState()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Frank {Guid.NewGuid():n}");
        await Inbox.CreateDocumentAsync("Invoice");
        var inboxAddress = Page.Url;

        var other = await Context.NewPageAsync();
        await other.GotoAsync(inboxAddress);
        await new InboxPage(other).RenameDocumentAsync("Invoice", "Newer title");

        // This tab still holds the old version of the document.
        await Inbox.DocumentRow("Invoice").GetByRole(AriaRole.Button, new() { Name = "Rename" }).ClickAsync();
        await Inbox.Dialog.GetByLabel("Title").FillAsync("Late title");
        await Inbox.Dialog.GetByRole(AriaRole.Button, new() { Name = "Rename document", Exact = true }).ClickAsync();

        await Expect(Inbox.Dialog).ToContainTextAsync("This item was changed somewhere else.");
        await Inbox.Dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Expect(Inbox.DocumentRow("Newer title")).ToBeVisibleAsync();
        await Expect(Inbox.DocumentRow("Late title")).ToBeHiddenAsync();
    }

    [Test]
    public async Task TheSameStaleRename_ExplainsWhy_OnAnArchivesPage()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Gina {Guid.NewGuid():n}");
        await Sidebar.CreateArchiveAsync("Taxes");
        await Inbox.CreateDocumentAsync("Invoice");
        await Inbox.FileDocumentIntoAsync("Invoice", "Taxes");
        await OpenArchiveAsync("Taxes");
        var archiveAddress = Page.Url;

        var other = await Context.NewPageAsync();
        await other.GotoAsync(archiveAddress);
        await new ArchivePage(other).RenameDocumentAsync("Invoice", "Newer title");

        await Archive.DocumentRow("Invoice").GetByRole(AriaRole.Button, new() { Name = "Rename" }).ClickAsync();
        await Archive.Dialog.GetByLabel("Title").FillAsync("Late title");
        await Archive.Dialog.GetByRole(AriaRole.Button, new() { Name = "Rename document", Exact = true }).ClickAsync();

        await Expect(Archive.Dialog).ToContainTextAsync("This item was changed somewhere else.");
    }

    [Test]
    public async Task DownloadingADocument_DeliversTheFileUnderItsNameWithItsContent()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Xena {Guid.NewGuid():n}");
        // Larger than one 64 KB chunk, and not made of one repeated byte.
        var content = new byte[200_000];
        new Random(42).NextBytes(content);
        await Inbox.UploadAsync(new InboxPage.UploadFile("scan.pdf", content));
        await Expect(Inbox.UploadState("scan.pdf")).ToHaveTextAsync("Done");
        await Inbox.CloseUploadDialogAsync();

        var download = await Inbox.DownloadAsync("scan");

        await Assert.That(download.SuggestedFilename).IsEqualTo("scan.pdf");
        var path = await download.PathAsync() ?? throw new InvalidOperationException("The download has no file.");
        await Assert.That(await File.ReadAllBytesAsync(path)).IsEquivalentTo(content);
    }

    /// <summary>Opens an archive from the sidebar and returns the address of its page.</summary>
    private async Task<string> OpenArchiveAsync(string name)
    {
        await Sidebar.ArchiveEntry(name).ClickAsync();
        await Expect(Archive.Heading).ToHaveTextAsync(name);

        return Page.Url;
    }
}
