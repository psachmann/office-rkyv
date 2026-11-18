using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// An archive's page as a person uses it: its documents, and the actions that rename or delete the archive. It
/// offers no new-document action: a new document goes to the inbox.
/// </summary>
public sealed class ArchivePage(IPage page) : DocumentListPage(page)
{
    public ILocator RenameArchiveButton(string name) =>
        page.GetByRole(AriaRole.Button, new() { Name = $"Rename archive {name}" });

    public ILocator DeleteArchiveButton(string name) =>
        page.GetByRole(AriaRole.Button, new() { Name = $"Delete archive {name}" });

    public ILocator NewDocumentButton => page.GetByRole(AriaRole.Button, new() { Name = "New document" });

    public async Task RenameArchiveAsync(string name, string newName)
    {
        await RenameArchiveButton(name).ClickAsync();
        await Dialog.GetByLabel("Name").FillAsync(newName);
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Rename archive", Exact = true }).ClickAsync();
        await Expect(Dialog).ToBeHiddenAsync();
    }

    /// <summary>Deletes the archive this page shows, after confirming that its documents return to the inbox.</summary>
    public async Task DeleteArchiveAsync(string name)
    {
        await DeleteArchiveButton(name).ClickAsync();
        await ConfirmDeleteAsync();
    }
}
