using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The header's search control and the search dialog it opens, as a person uses them. The control and the field
/// are found by role and accessible name; a result's location, which has no name of its own, by test id.
/// </summary>
public sealed class SearchDialogPage(IPage page)
{
    private const string Name = "Search documents";

    /// <summary>The control in the middle of the header; there is none while nobody is signed in.</summary>
    public ILocator Control => page.Locator(".rz-header").GetByRole(AriaRole.Button, new() { Name = Name });

    public ILocator Dialog => page.GetByRole(AriaRole.Dialog, new() { Name = Name });

    public ILocator Field => Dialog.GetByRole(AriaRole.Textbox, new() { Name = Name });

    public ILocator Hint => Dialog.GetByTestId("search-hint");

    public ILocator NoResults => Dialog.GetByTestId("search-no-results");

    public ILocator More => Dialog.GetByTestId("search-more");

    public ILocator Failed => Dialog.GetByTestId("search-failed");

    public ILocator DownloadError => Dialog.GetByTestId("search-download-error");

    /// <summary>Every result, in the order the dialog lists them: the button that opens the document's place.</summary>
    public ILocator Results => Dialog.Locator("[data-search-result]");

    /// <summary>
    /// The result of the document with this title. A result's name holds the file name and the location as well,
    /// so the match is on the start of the name.
    /// </summary>
    public ILocator Result(string title) =>
        Results.Filter(new() { Has = page.GetByText(title, new() { Exact = true }) });

    /// <summary>Where a result says its document is: the inbox, or the archive's name.</summary>
    public ILocator LocationOf(string title) => Result(title).GetByTestId("search-result-location");

    public ILocator DownloadButton(string title) =>
        Dialog.GetByRole(AriaRole.Button, new() { Name = $"Download {title}", Exact = true });

    /// <summary>
    /// The page is rendered on the server first and only becomes interactive once its circuit connects, and a click
    /// or a key before that does nothing. The action is repeated until the dialog is there: doing it again with the
    /// dialog open is harmless.
    /// </summary>
    private async Task UntilOpenAsync(Func<Task> open)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            // A slow first attempt may have opened it after all; a click would then hit the dialog's overlay.
            if (await Dialog.IsVisibleAsync())
            {
                return;
            }

            try
            {
                await open();
                await Expect(Dialog).ToBeVisibleAsync(new() { Timeout = 2000 });

                return;
            }
            catch (PlaywrightException)
            {
                // Not interactive yet: again.
            }
        }

        throw new TimeoutException("The search dialog never opened.");
    }

    public Task OpenWithTheControlAsync() => UntilOpenAsync(() => Control.ClickAsync(new() { Timeout = 2000 }));

    public Task OpenWithTheShortcutAsync() => UntilOpenAsync(() => page.Keyboard.PressAsync("Control+K"));

    /// <summary>Types into the field in one go; the dialog runs the search once typing has rested.</summary>
    public Task SearchAsync(string text) => Field.FillAsync(text);

    public async Task CloseAsync()
    {
        await page.Keyboard.PressAsync("Escape");
        await Expect(Dialog).ToBeHiddenAsync();
    }

    /// <summary>Chooses a result's download and returns the download the browser received.</summary>
    public Task<IDownload> DownloadAsync(string title) =>
        page.RunAndWaitForDownloadAsync(async () => await DownloadButton(title).ClickAsync());
}
