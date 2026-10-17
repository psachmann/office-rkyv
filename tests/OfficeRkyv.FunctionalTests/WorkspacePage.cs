using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The user workspace as a person uses it: what is on the page and the actions on it. Elements are found by
/// role, label or visible text where a person would, and by <c>data-testid</c> only for the parts that have
/// no name of their own (the navigation entries and the document rows).
/// </summary>
public sealed class WorkspacePage(IPage page)
{
    public ILocator Dialog => page.GetByRole(AriaRole.Dialog);

    public ILocator Heading => page.GetByTestId("workspace-heading");

    public ILocator NotFound => page.GetByTestId("workspace-not-found");

    public ILocator Error => page.GetByTestId("workspace-error");

    /// <summary>The name of what the main area shows: the selected archive, or the inbox.</summary>
    public ILocator Selection => page.GetByTestId("workspace-selection");

    public ILocator EmptyState => page.GetByTestId("empty-state");

    /// <summary>The search field above the document list, found by its accessible name as a person finds it.</summary>
    public ILocator SearchField => page.GetByRole(AriaRole.Textbox, new() { Name = "Search documents" });

    /// <summary>The hint that asks for more characters instead of showing results.</summary>
    public ILocator SearchHint => page.GetByTestId("search-hint");

    public ILocator SearchNoResults => page.GetByTestId("search-no-results");

    /// <summary>The note that more documents matched than are shown.</summary>
    public ILocator SearchMore => page.GetByTestId("search-more");

    /// <summary>The heading of the results, shown instead of the selection's name while a search is entered.</summary>
    public ILocator SearchHeading => page.GetByTestId("search-heading");

    public ILocator Sidebar => page.Locator(".rz-sidebar");

    /// <summary>The sidebar link that holds the inbox entry, so it carries the link's own state and click.</summary>
    public ILocator InboxEntry => SidebarLink(page.GetByTestId("nav-inbox"));

    public ILocator InboxCount => page.GetByTestId("nav-inbox-count");

    public ILocator ArchiveEntry(string name) =>
        SidebarLink(page.GetByTestId("nav-archive").Filter(new() { HasText = name }));

    public ILocator ArchiveEntries => SidebarLink(page.GetByTestId("nav-archive"));

    /// <summary>Every link in the sidebar that leads into a workspace's inbox or archives.</summary>
    public ILocator WorkspaceEntries => SidebarLink(page.Locator("[data-testid=nav-inbox], [data-testid=nav-archive]"));

    public ILocator RenameArchiveButton(string name) =>
        page.GetByRole(AriaRole.Button, new() { Name = $"Rename archive {name}" });

    public ILocator DeleteArchiveButton(string name) =>
        page.GetByRole(AriaRole.Button, new() { Name = $"Delete archive {name}" });

    private ILocator SidebarLink(ILocator inside) => Sidebar.GetByRole(AriaRole.Link).Filter(new() { Has = inside });

    /// <summary>
    /// The row of the document with exactly this title. The file name is shown in the row too, and it keeps
    /// the name the file was uploaded with when the title is renamed, so a text match on the row would find both.
    /// </summary>
    public ILocator DocumentRow(string title) =>
        page.GetByTestId("document-row")
            .Filter(new() { Has = page.Locator("td").GetByText(title, new() { Exact = true }) });

    /// <summary>Where a listed document is, as the results say: the inbox, or the archive's name.</summary>
    public ILocator DocumentLocation(string title) => DocumentRow(title).GetByTestId("document-location");

    /// <summary>Types into the search field in one go; the page runs the search once typing has rested.</summary>
    public Task SearchAsync(string text) => SearchField.FillAsync(text);

    /// <summary>Creates a user on the landing page and opens their workspace from the user list.</summary>
    public async Task OpenNewUsersWorkspaceAsync(string baseAddress, string displayName)
    {
        await page.GotoAsync(baseAddress);
        await page.GetByRole(AriaRole.Button, new() { Name = "New user" }).First.ClickAsync();
        await page.GetByLabel("Email").FillAsync($"{Guid.NewGuid():n}@example.com");
        await page.GetByLabel("Display name").FillAsync(displayName);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create user" }).ClickAsync();

        await page.GetByRole(AriaRole.Link, new() { Name = displayName, Exact = true }).ClickAsync();
        await Expect(Heading).ToHaveTextAsync(displayName);
    }

    public async Task CreateArchiveAsync(string name)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "New archive" }).ClickAsync();
        await Dialog.GetByLabel("Name").FillAsync(name);
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Create archive" }).ClickAsync();
        await Expect(Dialog).ToBeHiddenAsync();
    }

    /// <summary>One chosen file: its name, its bytes and the type the browser would declare for it.</summary>
    public sealed record UploadFile(string Name, byte[] Content, string MimeType = "application/pdf");

    /// <summary>The files of the open upload dialog, each with its state.</summary>
    public ILocator UploadItems => Dialog.GetByTestId("upload-item");

    /// <summary>The state text of one file in the open upload dialog, found by the file's name.</summary>
    public ILocator UploadState(string fileName) =>
        UploadItems.Filter(new() { HasText = fileName }).GetByTestId("upload-state");

    /// <summary>
    /// Opens the new-document action, chooses the files and waits until each has been uploaded or refused.
    /// The dialog stays open, so a test can look at what each file's state says.
    /// </summary>
    public async Task UploadAsync(params UploadFile[] files)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "New document" }).ClickAsync();
        await Dialog
            .GetByLabel("Choose files")
            .SetInputFilesAsync(
                files
                    .Select(file => new FilePayload
                    {
                        Name = file.Name,
                        MimeType = file.MimeType,
                        Buffer = file.Content,
                    })
                    .ToArray()
            );

        // The dialog cannot be closed while a file is still going up.
        await Expect(CloseUploadButton).ToBeEnabledAsync();
    }

    private ILocator CloseUploadButton => Dialog.Locator("button").Filter(new() { HasTextString = "Close" });

    public async Task CloseUploadDialogAsync()
    {
        await CloseUploadButton.ClickAsync();
        await Expect(Dialog).ToBeHiddenAsync();
    }

    /// <summary>
    /// Gives the inbox a document with this title: a small file of that name is uploaded, and the server
    /// titles it from the file name.
    /// </summary>
    public async Task CreateDocumentAsync(string title)
    {
        await UploadAsync(new UploadFile($"{title}.pdf", System.Text.Encoding.UTF8.GetBytes($"%PDF-1.4 {title}")));
        await CloseUploadDialogAsync();
    }

    /// <summary>Chooses a document's download action and returns the download the browser received.</summary>
    public Task<IDownload> DownloadAsync(string title) =>
        page.RunAndWaitForDownloadAsync(async () =>
            await DocumentRow(title).GetByRole(AriaRole.Button, new() { Name = "Download" }).ClickAsync()
        );

    /// <summary>Opens a document's "File into..." choice and picks an entry from it.</summary>
    public async Task FileDocumentIntoAsync(string title, string target)
    {
        await DocumentRow(title).GetByRole(AriaRole.Button, new() { Name = "File into..." }).ClickAsync();
        await page.Locator("ul.rz-menu")
            .GetByRole(AriaRole.Menuitem, new() { Name = target, Exact = true })
            .ClickAsync();
    }

    public async Task RenameArchiveAsync(string name, string newName)
    {
        await ArchiveEntry(name).ClickAsync();
        await RenameArchiveButton(name).ClickAsync();
        await Dialog.GetByLabel("Name").FillAsync(newName);
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Rename archive", Exact = true }).ClickAsync();
        await Expect(Dialog).ToBeHiddenAsync();
    }

    public async Task RenameDocumentAsync(string title, string newTitle)
    {
        await DocumentRow(title).GetByRole(AriaRole.Button, new() { Name = "Rename" }).ClickAsync();
        await Dialog.GetByLabel("Title").FillAsync(newTitle);
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Rename document", Exact = true }).ClickAsync();
        await Expect(Dialog).ToBeHiddenAsync();
    }

    public async Task DeleteArchiveAsync(string name)
    {
        await ArchiveEntry(name).ClickAsync();
        await DeleteArchiveButton(name).ClickAsync();
        await ConfirmDeleteAsync();
    }

    public async Task DeleteDocumentAsync(string title)
    {
        await DocumentRow(title).GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await ConfirmDeleteAsync();
    }

    // Radzen shows a confirmation as an alert dialog, unlike the create and rename forms.
    public ILocator ConfirmationDialog => page.GetByRole(AriaRole.Alertdialog);

    private async Task ConfirmDeleteAsync()
    {
        await ConfirmationDialog.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
        await Expect(ConfirmationDialog).ToBeHiddenAsync();
    }
}
