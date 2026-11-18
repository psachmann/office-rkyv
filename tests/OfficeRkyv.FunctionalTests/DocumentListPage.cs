using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// A page that lists documents, as a person uses it on the inbox and on an archive page: the documents, and the
/// actions on each. The inbox and the archive page build on it.
/// </summary>
public class DocumentListPage(IPage page)
{
    /// <summary>The name of what the page shows: the inbox, or the archive's name.</summary>
    public ILocator Heading => page.GetByTestId("page-heading");

    /// <summary>The alert a refusal is shown in, the one place on the page.</summary>
    public ILocator Error => page.GetByTestId("page-error");

    public ILocator EmptyState => page.GetByTestId("empty-state");

    public ILocator Dialog => page.GetByRole(AriaRole.Dialog);

    /// <summary>Radzen shows a confirmation as an alert dialog, unlike the create and rename forms.</summary>
    public ILocator ConfirmationDialog => page.GetByRole(AriaRole.Alertdialog);

    /// <summary>
    /// The row of the document with exactly this title. The file name is shown in the row too, and it keeps the
    /// name the file was uploaded with when the title is renamed, so a text match on the row would find both.
    /// </summary>
    public ILocator DocumentRow(string title) =>
        page.GetByTestId("document-row")
            .Filter(new() { Has = page.Locator("td").GetByText(title, new() { Exact = true }) });

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

    public async Task RenameDocumentAsync(string title, string newTitle)
    {
        await DocumentRow(title).GetByRole(AriaRole.Button, new() { Name = "Rename" }).ClickAsync();
        await Dialog.GetByLabel("Title").FillAsync(newTitle);
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Rename document", Exact = true }).ClickAsync();
        await Expect(Dialog).ToBeHiddenAsync();
    }

    public async Task DeleteDocumentAsync(string title)
    {
        await DocumentRow(title).GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await ConfirmDeleteAsync();
    }

    protected async Task ConfirmDeleteAsync()
    {
        await ConfirmationDialog.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
        await Expect(ConfirmationDialog).ToBeHiddenAsync();
    }
}
