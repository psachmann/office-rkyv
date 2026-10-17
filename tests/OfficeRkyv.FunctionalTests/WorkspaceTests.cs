using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The user workspace against the real app. One AppHost (and so one SQLite database) is shared by the whole
/// session, so every test creates its own user and never looks at anyone else's data.
/// </summary>
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public class WorkspaceTests(AppHostFixture fixture) : PageTest
{
    private WorkspacePage Workspace => new(Page);

    private string BaseAddress =>
        fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri
        ?? throw new InvalidOperationException("The web service has no address.");

    [Test]
    public async Task CreatingAnArchiveAndADocument_FilingIt_AndDeletingTheArchive_ReturnsTheDocumentToTheInbox()
    {
        var name = $"Alice {Guid.NewGuid():n}";
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, name);

        // A new user starts with the inbox selected and nothing in it.
        await Expect(Workspace.InboxEntry).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Workspace.InboxCount).ToHaveTextAsync("0");
        await Expect(Workspace.EmptyState).ToBeVisibleAsync();

        await Workspace.CreateArchiveAsync("Household");
        await Expect(Workspace.ArchiveEntry("Household")).ToBeVisibleAsync();

        // A new document lands in the inbox, without a reload.
        await Workspace.CreateDocumentAsync("Invoice March");
        await Expect(Workspace.DocumentRow("Invoice March")).ToBeVisibleAsync();
        await Expect(Workspace.InboxCount).ToHaveTextAsync("1");

        // Filing takes it out of the inbox and into the archive.
        await Workspace.FileDocumentIntoAsync("Invoice March", "Household");
        await Expect(Workspace.DocumentRow("Invoice March")).ToBeHiddenAsync();
        await Expect(Workspace.InboxCount).ToHaveTextAsync("0");

        await Workspace.ArchiveEntry("Household").ClickAsync();
        await Expect(Workspace.ArchiveEntry("Household")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Workspace.DocumentRow("Invoice March")).ToBeVisibleAsync();

        // Deleting the archive says where its documents go, selects the inbox, and the document comes back.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete archive Household" }).ClickAsync();
        await Expect(Workspace.ConfirmationDialog).ToContainTextAsync("return to the inbox");
        await Workspace
            .ConfirmationDialog.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true })
            .ClickAsync();

        await Expect(Workspace.ArchiveEntry("Household")).ToBeHiddenAsync();
        await Expect(Workspace.InboxEntry).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Workspace.DocumentRow("Invoice March")).ToBeVisibleAsync();
        await Expect(Workspace.InboxCount).ToHaveTextAsync("1");
    }

    [Test]
    public async Task AnUnknownUser_ShowsNotFound_InsteadOfAWorkspace()
    {
        await Page.GotoAsync($"{BaseAddress}users/{Guid.NewGuid()}");

        await Expect(Workspace.NotFound).ToBeVisibleAsync();
        await Expect(Workspace.Heading).ToBeHiddenAsync();
    }

    [Test]
    public async Task TheDialogs_FlagMissingInput_AndShowTheServersRefusalInTheUsersLanguage()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Bob {Guid.NewGuid():n}");

        // A missing name is flagged before anything is sent; the dialog stays open.
        await Page.GetByRole(AriaRole.Button, new() { Name = "New archive" }).ClickAsync();
        await Workspace.Dialog.GetByRole(AriaRole.Button, new() { Name = "Create archive" }).ClickAsync();
        await Expect(Workspace.Dialog.GetByText("Enter a name.")).ToBeVisibleAsync();

        // Too long is only known to the server, whose refusal comes back as a localized message.
        await Workspace.Dialog.GetByLabel("Name").FillAsync(new string('n', 101));
        await Workspace.Dialog.GetByRole(AriaRole.Button, new() { Name = "Create archive" }).ClickAsync();
        await Expect(Workspace.Dialog.GetByText("This name is too long.")).ToBeVisibleAsync();

        // Correcting it in the same dialog works.
        await Workspace.Dialog.GetByLabel("Name").FillAsync("Household");
        await Workspace.Dialog.GetByRole(AriaRole.Button, new() { Name = "Create archive" }).ClickAsync();
        await Expect(Workspace.ArchiveEntry("Household")).ToBeVisibleAsync();

        // A document needs a file: the server refuses an empty one, and the dialog says so in the user's language.
        await Workspace.UploadAsync(new WorkspacePage.UploadFile("empty.txt", [], "text/plain"));
        await Expect(Workspace.UploadState("empty.txt")).ToHaveTextAsync("This file is empty.");
    }

    [Test]
    public async Task RenamingArchivesAndDocuments_UpdatesTheListsWithoutAReload()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Carol {Guid.NewGuid():n}");
        await Workspace.CreateArchiveAsync("Taxes");
        await Workspace.CreateDocumentAsync("Invoice March");

        await Workspace.RenameArchiveAsync("Taxes", "Taxes 2026");
        await Expect(Workspace.ArchiveEntry("Taxes 2026")).ToBeVisibleAsync();
        await Expect(Workspace.ArchiveEntry("Taxes 2026")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Workspace.Selection).ToHaveTextAsync("Taxes 2026");

        // The document is in the inbox, which the renamed archive being selected no longer shows.
        await Workspace.InboxEntry.ClickAsync();

        await Workspace.RenameDocumentAsync("Invoice March", "Invoice April");
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeVisibleAsync();
        await Expect(Workspace.DocumentRow("Invoice March")).ToBeHiddenAsync();
    }

    [Test]
    public async Task UnfilingADocument_ReturnsItToTheInbox_AndDeletingItRemovesItEverywhere()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Dave {Guid.NewGuid():n}");
        await Workspace.CreateArchiveAsync("Household");
        await Workspace.CreateDocumentAsync("Lease");
        await Workspace.FileDocumentIntoAsync("Lease", "Household");
        await Workspace.ArchiveEntry("Household").ClickAsync();
        await Expect(Workspace.DocumentRow("Lease")).ToBeVisibleAsync();

        // From inside the archive, the choice offers the inbox (and no other archive exists here).
        await Workspace.FileDocumentIntoAsync("Lease", "Inbox");
        await Expect(Workspace.DocumentRow("Lease")).ToBeHiddenAsync();
        await Expect(Workspace.InboxCount).ToHaveTextAsync("1");

        await Workspace.InboxEntry.ClickAsync();
        await Workspace.DeleteDocumentAsync("Lease");
        await Expect(Workspace.DocumentRow("Lease")).ToBeHiddenAsync();
        await Expect(Workspace.InboxCount).ToHaveTextAsync("0");
        await Expect(Workspace.EmptyState).ToBeVisibleAsync();
    }

    [Test]
    public async Task FilingIntoAnArchiveDeletedElsewhere_ExplainsWhy_AndTheArchiveDisappears()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Erin {Guid.NewGuid():n}");
        await Workspace.CreateArchiveAsync("Household");
        await Workspace.CreateDocumentAsync("Invoice");

        // Another browser tab deletes the archive while this one still shows it.
        var other = await Context.NewPageAsync();
        await other.GotoAsync(Page.Url);
        var otherWorkspace = new WorkspacePage(other);
        await Expect(otherWorkspace.ArchiveEntry("Household")).ToBeVisibleAsync();
        await otherWorkspace.DeleteArchiveAsync("Household");
        await Expect(otherWorkspace.ArchiveEntry("Household")).ToBeHiddenAsync();

        await Workspace.FileDocumentIntoAsync("Invoice", "Household");

        await Expect(Workspace.Error).ToContainTextAsync("This archive has been deleted.");
        await Expect(Workspace.ArchiveEntry("Household")).ToBeHiddenAsync();
        await Expect(Workspace.DocumentRow("Invoice")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ARenameOfADocumentChangedElsewhere_ExplainsWhy_AndShowsTheCurrentState()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Frank {Guid.NewGuid():n}");
        await Workspace.CreateDocumentAsync("Invoice");

        var other = await Context.NewPageAsync();
        await other.GotoAsync(Page.Url);
        var otherWorkspace = new WorkspacePage(other);
        await otherWorkspace.RenameDocumentAsync("Invoice", "Newer title");

        // This tab still holds the old version of the document.
        await Workspace.DocumentRow("Invoice").GetByRole(AriaRole.Button, new() { Name = "Rename" }).ClickAsync();
        await Workspace.Dialog.GetByLabel("Title").FillAsync("Late title");
        await Workspace
            .Dialog.GetByRole(AriaRole.Button, new() { Name = "Rename document", Exact = true })
            .ClickAsync();

        await Expect(Workspace.Dialog).ToContainTextAsync("This item was changed somewhere else.");
        await Workspace.Dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Expect(Workspace.DocumentRow("Newer title")).ToBeVisibleAsync();
        await Expect(Workspace.DocumentRow("Late title")).ToBeHiddenAsync();
    }

    [Test]
    public async Task TheEntries_AreInTheSidebarAboveSettings_AndThePageHoldsNoOtherNavigation()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Gina {Guid.NewGuid():n}");
        await Workspace.CreateArchiveAsync("Household");

        await Expect(Workspace.Sidebar.GetByTestId("nav-inbox")).ToBeVisibleAsync();
        await Expect(Workspace.Sidebar.GetByTestId("nav-inbox-count")).ToHaveTextAsync("0");
        await Expect(Workspace.Sidebar.GetByTestId("nav-archive").Filter(new() { HasText = "Household" }))
            .ToBeVisibleAsync();

        var body = Page.Locator(".rz-body");
        await Expect(body.GetByRole(AriaRole.Navigation)).ToHaveCountAsync(0);
        await Expect(body.GetByTestId("nav-inbox")).ToHaveCountAsync(0);
        await Expect(body.GetByTestId("nav-archive")).ToHaveCountAsync(0);

        var household = await Workspace.ArchiveEntry("Household").BoundingBoxAsync();
        var settings = await new SettingsPage(Page).SidebarEntry("Settings").BoundingBoxAsync();
        await Assert.That(household!.Y + household.Height).IsLessThanOrEqualTo(settings!.Y);
    }

    [Test]
    public async Task LeavingTheWorkspace_ForTheLandingPageOrSettings_LeavesNoEntriesInTheSidebar()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Hank {Guid.NewGuid():n}");
        await Workspace.CreateArchiveAsync("Household");
        await Expect(Workspace.WorkspaceEntries).ToHaveCountAsync(2);

        var settings = new SettingsPage(Page);
        await settings.SidebarEntry("Home").ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Users" })).ToBeVisibleAsync();
        await Expect(Workspace.WorkspaceEntries).ToHaveCountAsync(0);

        await Page.GoBackAsync();
        await Expect(Workspace.WorkspaceEntries).ToHaveCountAsync(2);

        await settings.SidebarEntry("Settings").ClickAsync();
        await Expect(settings.Heading).ToBeVisibleAsync();
        await Expect(Workspace.WorkspaceEntries).ToHaveCountAsync(0);
    }

    [Test]
    public async Task UploadingAFile_CreatesADocumentTitledFromItsName_InTheInbox()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Ulla {Guid.NewGuid():n}");

        await Workspace.UploadAsync(new WorkspacePage.UploadFile("Invoice March.pdf", [1, 2, 3, 4, 5]));

        await Expect(Workspace.UploadState("Invoice March.pdf")).ToHaveTextAsync("Done");
        await Workspace.CloseUploadDialogAsync();
        await Expect(Workspace.DocumentRow("Invoice March")).ToBeVisibleAsync();
        await Expect(Workspace.InboxCount).ToHaveTextAsync("1");
    }

    [Test]
    public async Task UploadingSeveralFiles_CreatesOneDocumentForEach()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Vera {Guid.NewGuid():n}");

        await Workspace.UploadAsync(
            new WorkspacePage.UploadFile("invoice.pdf", [1, 2, 3]),
            new WorkspacePage.UploadFile("lease.pdf", [4, 5, 6])
        );

        await Expect(Workspace.UploadState("invoice.pdf")).ToHaveTextAsync("Done");
        await Expect(Workspace.UploadState("lease.pdf")).ToHaveTextAsync("Done");
        await Workspace.CloseUploadDialogAsync();
        await Expect(Workspace.DocumentRow("invoice")).ToBeVisibleAsync();
        await Expect(Workspace.DocumentRow("lease")).ToBeVisibleAsync();
        await Expect(Workspace.InboxCount).ToHaveTextAsync("2");
    }

    [Test]
    public async Task ARefusedFile_IsShownAsRefused_AndDoesNotStopTheOthers()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Wilma {Guid.NewGuid():n}");

        await Workspace.UploadAsync(
            new WorkspacePage.UploadFile("empty.txt", [], "text/plain"),
            new WorkspacePage.UploadFile("lease.pdf", [4, 5, 6])
        );

        await Expect(Workspace.UploadState("empty.txt")).ToHaveTextAsync("This file is empty.");
        await Expect(Workspace.UploadState("lease.pdf")).ToHaveTextAsync("Done");
        await Workspace.CloseUploadDialogAsync();
        await Expect(Workspace.DocumentRow("lease")).ToBeVisibleAsync();
        await Expect(Workspace.InboxCount).ToHaveTextAsync("1");
    }

    [Test]
    public async Task DownloadingADocument_DeliversTheFileUnderItsNameWithItsContent()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Xena {Guid.NewGuid():n}");
        // Larger than one 64 KB chunk, and not made of one repeated byte.
        var content = new byte[200_000];
        new Random(42).NextBytes(content);
        await Workspace.UploadAsync(new WorkspacePage.UploadFile("scan.pdf", content));
        await Expect(Workspace.UploadState("scan.pdf")).ToHaveTextAsync("Done");
        await Workspace.CloseUploadDialogAsync();

        var download = await Workspace.DownloadAsync("scan");

        await Assert.That(download.SuggestedFilename).IsEqualTo("scan.pdf");
        var path = await download.PathAsync() ?? throw new InvalidOperationException("The download has no file.");
        await Assert.That(await File.ReadAllBytesAsync(path)).IsEquivalentTo(content);
    }

    [Test]
    public async Task AnUnknownUser_ShowsNoEntriesInTheSidebar()
    {
        await Page.GotoAsync($"{BaseAddress}users/{Guid.NewGuid()}");

        await Expect(Workspace.NotFound).ToBeVisibleAsync();
        await Expect(Workspace.WorkspaceEntries).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "New archive" })).ToHaveCountAsync(0);
    }

    [Test]
    public async Task ArchiveActions_AreOfferedOnlyWhileAnArchiveIsSelected()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Iris {Guid.NewGuid():n}");
        await Workspace.CreateArchiveAsync("Taxes");

        await Expect(Workspace.InboxEntry).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Workspace.RenameArchiveButton("Taxes")).ToHaveCountAsync(0);
        await Expect(Workspace.DeleteArchiveButton("Taxes")).ToHaveCountAsync(0);

        await Workspace.ArchiveEntry("Taxes").ClickAsync();
        await Expect(Workspace.RenameArchiveButton("Taxes")).ToBeVisibleAsync();
        await Expect(Workspace.DeleteArchiveButton("Taxes")).ToBeVisibleAsync();

        await Workspace.InboxEntry.ClickAsync();
        await Expect(Workspace.RenameArchiveButton("Taxes")).ToHaveCountAsync(0);
        await Expect(Workspace.DeleteArchiveButton("Taxes")).ToHaveCountAsync(0);
    }

    // ---- search ----

    // Invoice March and Lease in the inbox, Invoice April in the archive Taxes 2026.
    private async Task OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync(string user)
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"{user} {Guid.NewGuid():n}");
        await Workspace.CreateArchiveAsync("Taxes 2026");
        await Workspace.CreateDocumentAsync("Invoice April");
        await Workspace.FileDocumentIntoAsync("Invoice April", "Taxes 2026");
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeHiddenAsync();
        await Workspace.CreateDocumentAsync("Invoice March");
        await Workspace.CreateDocumentAsync("Lease");
        await Expect(Workspace.InboxCount).ToHaveTextAsync("2");
    }

    [Test]
    public async Task Searching_FindsDocumentsInTheInboxAndInArchives_AndSaysWhereEachIs()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Judith");

        await Workspace.SearchAsync("invoice");

        await Expect(Workspace.DocumentRow("Invoice March")).ToBeVisibleAsync();
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeVisibleAsync();
        await Expect(Workspace.DocumentRow("Lease")).ToBeHiddenAsync();
        await Expect(Workspace.DocumentLocation("Invoice March")).ToHaveTextAsync("Inbox");
        await Expect(Workspace.DocumentLocation("Invoice April")).ToHaveTextAsync("Taxes 2026");
        await Expect(Workspace.SearchHeading).ToBeVisibleAsync();
    }

    [Test]
    public async Task Searching_WhileAnArchiveIsSelected_StillFindsTheInbox()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Kai");
        await Workspace.ArchiveEntry("Taxes 2026").ClickAsync();
        await Expect(Workspace.Selection).ToHaveTextAsync("Taxes 2026");

        await Workspace.SearchAsync("lease");

        await Expect(Workspace.DocumentRow("Lease")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AWordThatMatchesNothing_ShowsTheNoResultsMessage_NotTheEmptyInbox()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Lena {Guid.NewGuid():n}");

        await Workspace.SearchAsync("garden");

        await Expect(Workspace.SearchNoResults).ToBeVisibleAsync();
        await Expect(Workspace.EmptyState).ToBeHiddenAsync();
    }

    [Test]
    public async Task AQueryOfTwoCharacters_ShowsTheHint_AndNoResults()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Mira");

        await Workspace.SearchAsync("in");

        await Expect(Workspace.SearchHint).ToBeVisibleAsync();
        await Expect(Workspace.DocumentRow("Invoice March")).ToBeHiddenAsync();
        await Expect(Workspace.SearchNoResults).ToBeHiddenAsync();
    }

    [Test]
    public async Task ClearingTheSearch_ShowsTheSelectedListAgain()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Nora");
        await Workspace.SearchAsync("invoice");
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeVisibleAsync();

        await Workspace.SearchAsync("");

        await Expect(Workspace.Selection).ToHaveTextAsync("Inbox");
        await Expect(Workspace.DocumentRow("Invoice March")).ToBeVisibleAsync();
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeHiddenAsync();
    }

    [Test]
    public async Task SelectingAnEntry_ClearsTheSearch()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Otto");
        await Workspace.SearchAsync("invoice");
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeVisibleAsync();

        await Workspace.ArchiveEntry("Taxes 2026").ClickAsync();

        await Expect(Workspace.SearchField).ToHaveValueAsync("");
        await Expect(Workspace.Selection).ToHaveTextAsync("Taxes 2026");
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeVisibleAsync();
    }

    [Test]
    public async Task DeletingAResult_RemovesItFromTheResults_WithoutAReload()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Pia");
        await Workspace.SearchAsync("invoice");
        // Invoice April is in an archive, so it is only listed once the search has run.
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeVisibleAsync();

        await Workspace.DeleteDocumentAsync("Invoice March");

        await Expect(Workspace.DocumentRow("Invoice March")).ToBeHiddenAsync();
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeVisibleAsync();
    }

    [Test]
    public async Task TheSearch_IsKeptInTheAddress_SoAReloadAndAnOpenedAddressKeepIt()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Quinn");
        var workspaceAddress = Page.Url;

        await Workspace.SearchAsync("invoice");
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("[?&]q=invoice$"));

        await Page.ReloadAsync();
        await Expect(Workspace.SearchField).ToHaveValueAsync("invoice");
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeVisibleAsync();

        await Page.GotoAsync($"{workspaceAddress}?q=lease");
        await Expect(Workspace.SearchField).ToHaveValueAsync("lease");
        await Expect(Workspace.DocumentRow("Lease")).ToBeVisibleAsync();
    }

    [Test]
    public async Task GoingBack_LeavesTheSearch_NotOneCharacterOfIt()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Rosa");

        // The first query adds a history entry, the change after it replaces that entry.
        await Workspace.SearchAsync("invoice");
        await Expect(Page).ToHaveURLAsync(new Regex("[?&]q=invoice$"));
        await Workspace.SearchAsync("invoice march");
        await Expect(Page).ToHaveURLAsync(new Regex("[?&]q=invoice(%20|\\+)march$"));

        await Page.GoBackAsync();

        await Expect(Workspace.SearchField).ToHaveValueAsync("");
        await Expect(Workspace.Selection).ToHaveTextAsync("Inbox");
    }

    [Test]
    public async Task Typing_ThatANavigationInterrupts_IsNotAppliedToThePageThatWasOpened()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Sven");

        // The click comes before the search has been applied, so the typing belongs to the page that was left.
        await Workspace.SearchAsync("invoice");
        await Workspace.ArchiveEntry("Taxes 2026").ClickAsync();
        await Expect(Workspace.Selection).ToHaveTextAsync("Taxes 2026");

        // Longer than the page waits after typing: what must not happen is a search that arrives late.
        await Task.Delay(TimeSpan.FromSeconds(1));
        await Expect(Page).Not.ToHaveURLAsync(new Regex("[?&]q="));
        await Expect(Workspace.SearchField).ToHaveValueAsync("");
        await Expect(Workspace.Selection).ToHaveTextAsync("Taxes 2026");
    }
}
