using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The user workspace against the real app. One AppHost (and so one SQLite database) is shared by the whole
/// session, so every test creates its own user and never looks at anyone else's data.
/// </summary>
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AspireFixture<Projects.OfficeRkyv_AppHost>>(Shared = SharedType.PerTestSession)]
public class WorkspaceTests(AspireFixture<Projects.OfficeRkyv_AppHost> fixture) : PageTest
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

        await Page.GetByRole(AriaRole.Button, new() { Name = "New document" }).ClickAsync();
        await Workspace.Dialog.GetByRole(AriaRole.Button, new() { Name = "Create document" }).ClickAsync();
        await Expect(Workspace.Dialog.GetByText("Enter a title.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task RenamingArchivesAndDocuments_UpdatesTheListsWithoutAReload()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Carol {Guid.NewGuid():n}");
        await Workspace.CreateArchiveAsync("Taxes");
        await Workspace.CreateDocumentAsync("Invoice March");

        await Workspace.RenameArchiveAsync("Taxes", "Taxes 2026");
        await Expect(Workspace.ArchiveEntry("Taxes 2026")).ToBeVisibleAsync();

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
}
