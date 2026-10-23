using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The search from the header, against the real app: the control, the dialog and its results, choosing a result,
/// and the keyboard shortcut. Every test creates its own user, as the workspace tests do.
/// </summary>
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public class SearchTests(AppHostFixture fixture) : PageTest
{
    private WorkspacePage Workspace => new(Page);

    private SearchDialogPage Search => new(Page);

    private string BaseAddress =>
        fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri
        ?? throw new InvalidOperationException("The web service has no address.");

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

    // ---- the control and the dialog ----

    [Test]
    public async Task TheControl_IsInTheHeader_OnTheWorkspaceAndOnTheSettingsPage_AndOpensTheDialog()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Ada {Guid.NewGuid():n}");
        await Expect(Search.Control).ToBeVisibleAsync();
        await Expect(Search.Control).ToContainTextAsync("Ctrl+K");

        await Page.GotoAsync($"{BaseAddress}settings");
        await Expect(Search.Control).ToBeVisibleAsync();

        await Search.OpenWithTheControlAsync();
        await Expect(Search.Field).ToBeFocusedAsync();
    }

    [Test]
    public async Task TheControl_IsNotThereWhileNobodyIsSignedIn()
    {
        await Page.GotoAsync(BaseAddress);
        await Expect(Workspace.LandingHeading).ToBeVisibleAsync();

        await Expect(Search.Control).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Searching_FindsDocumentsInTheInboxAndInArchives_AndSaysWhereEachIs()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Judith");
        await Search.OpenWithTheControlAsync();

        await Search.SearchAsync("invoice");

        // Invoice April is in an archive, so it only appears once the search has run.
        await Expect(Search.Result("Invoice April")).ToBeVisibleAsync();
        await Expect(Search.Result("Invoice March")).ToBeVisibleAsync();
        await Expect(Search.Result("Lease")).ToHaveCountAsync(0);
        await Expect(Search.LocationOf("Invoice March")).ToHaveTextAsync("Inbox");
        await Expect(Search.LocationOf("Invoice April")).ToHaveTextAsync("Taxes 2026");
    }

    [Test]
    public async Task Searching_FromTheSettingsPage_FindsTheUsersDocuments()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Kai");
        await Page.GotoAsync($"{BaseAddress}settings");

        await Search.OpenWithTheControlAsync();
        await Search.SearchAsync("lease");

        await Expect(Search.Result("Lease")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AWordThatMatchesNothing_ShowsTheNoResultsMessage()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Lena {Guid.NewGuid():n}");
        await Search.OpenWithTheControlAsync();

        await Search.SearchAsync("garden");

        await Expect(Search.NoResults).ToBeVisibleAsync();
        await Expect(Search.Hint).ToBeHiddenAsync();
    }

    [Test]
    public async Task AnEmptyField_AndAQueryOfTwoCharacters_ShowTheHint_AndNoResults()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Mira");
        await Search.OpenWithTheControlAsync();
        await Expect(Search.Hint).ToBeVisibleAsync();

        await Search.SearchAsync("in");

        await Expect(Search.Hint).ToBeVisibleAsync();
        await Expect(Search.Results).ToHaveCountAsync(0);
        await Expect(Search.NoResults).ToBeHiddenAsync();
    }

    [Test]
    public async Task Escape_ClosesTheDialog_AndItOpensEmptyAgain()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Nora");
        await Search.OpenWithTheControlAsync();
        await Search.SearchAsync("invoice");
        await Expect(Search.Result("Invoice April")).ToBeVisibleAsync();

        await Search.CloseAsync();
        await Search.OpenWithTheControlAsync();

        await Expect(Search.Field).ToHaveValueAsync("");
        await Expect(Search.Results).ToHaveCountAsync(0);
        await Expect(Search.Hint).ToBeVisibleAsync();
    }

    [Test]
    public async Task TheQuery_IsNotKeptInTheAddress()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Otto");
        var address = Page.Url;
        await Search.OpenWithTheControlAsync();

        await Search.SearchAsync("invoice");

        await Expect(Search.Result("Invoice April")).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(address);
    }

    // ---- choosing a result ----

    [Test]
    public async Task ChoosingAResultInAnArchive_OpensThatArchive_AndClosesTheDialog()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Pia");
        await Search.OpenWithTheControlAsync();
        await Search.SearchAsync("invoice");

        await Search.Result("Invoice April").ClickAsync();

        await Expect(Search.Dialog).ToBeHiddenAsync();
        await Expect(Workspace.Selection).ToHaveTextAsync("Taxes 2026");
        await Expect(Workspace.DocumentRow("Invoice April")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChoosingAResultInTheInbox_FromAnotherPage_OpensTheInbox()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Quinn");
        await Page.GotoAsync($"{BaseAddress}settings");
        await Search.OpenWithTheControlAsync();
        await Search.SearchAsync("invoice march");

        await Search.Result("Invoice March").ClickAsync();

        await Expect(Search.Dialog).ToBeHiddenAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/workspace$"));
        await Expect(Workspace.Selection).ToHaveTextAsync("Inbox");
    }

    [Test]
    public async Task Enter_ChoosesTheFirstResult()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Rosa");
        await Search.OpenWithTheControlAsync();
        await Search.SearchAsync("invoice april");
        await Expect(Search.Result("Invoice April")).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Enter");

        await Expect(Search.Dialog).ToBeHiddenAsync();
        await Expect(Workspace.Selection).ToHaveTextAsync("Taxes 2026");
    }

    [Test]
    public async Task TheArrowKeys_MoveTheFocusThroughTheResults_AndBackToTheField()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Sven");
        await Search.OpenWithTheControlAsync();
        await Search.SearchAsync("invoice");
        await Expect(Search.Results).ToHaveCountAsync(2);

        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Search.Results.Nth(0)).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Search.Results.Nth(1)).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Search.Results.Nth(0)).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(Search.Field).ToBeFocusedAsync();

        // Down twice and Enter chooses the second result, whichever document the server ranked second.
        var second = await Search.Results.Nth(1).InnerTextAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Search.Dialog).ToBeHiddenAsync();
        var inTheArchive = second.Contains("Taxes 2026", StringComparison.Ordinal);
        await Expect(Workspace.Selection).ToHaveTextAsync(inTheArchive ? "Taxes 2026" : "Inbox");
    }

    [Test]
    public async Task DownloadingAResult_GivesTheOriginalFile_AndLeavesTheDialogOpen()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Tilda {Guid.NewGuid():n}");
        var content = System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 Invoice March");
        await Workspace.UploadAsync(new WorkspacePage.UploadFile("Invoice March.pdf", content));
        await Workspace.CloseUploadDialogAsync();
        await Search.OpenWithTheControlAsync();
        await Search.SearchAsync("invoice");
        await Expect(Search.Result("Invoice March")).ToBeVisibleAsync();

        var download = await Search.DownloadAsync("Invoice March");

        await Expect(Search.Dialog).ToBeVisibleAsync();
        await Assert.That(download.SuggestedFilename).IsEqualTo("Invoice March.pdf");
        await using var stream = await download.CreateReadStreamAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        await Assert.That(buffer.ToArray()).IsEquivalentTo(content);
    }

    // ---- the shortcut ----

    [Test]
    public async Task Ctrl_K_OpensTheDialog_OnTheWorkspaceAndOnTheSettingsPage_WithTheFieldFocused()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Uma {Guid.NewGuid():n}");

        await Search.OpenWithTheShortcutAsync();
        await Expect(Search.Field).ToBeFocusedAsync();
        await Search.CloseAsync();

        await Page.GotoAsync($"{BaseAddress}settings");
        await Search.OpenWithTheShortcutAsync();
        await Expect(Search.Field).ToBeFocusedAsync();
    }

    [Test]
    public async Task Ctrl_K_WhileTheDialogIsOpen_KeepsOneDialog_TheQuery_AndTheFocus()
    {
        await OpenWorkspaceWithDocumentsInTheInboxAndAnArchiveAsync("Vera");
        await Search.OpenWithTheShortcutAsync();
        await Search.SearchAsync("invoice");
        await Expect(Search.Result("Invoice April")).ToBeVisibleAsync();

        // Move the focus off the field, then press the shortcut: the field gets it back.
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(Search.Results.First).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Control+K");

        await Expect(Search.Field).ToBeFocusedAsync();
        await Expect(Search.Field).ToHaveValueAsync("invoice");
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Ctrl_K_WhileAnotherDialogIsOpen_OpensNothing()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Willi {Guid.NewGuid():n}");
        // Once the shortcut works for this page, it is registered; then the upload dialog is opened.
        await Search.OpenWithTheShortcutAsync();
        await Search.CloseAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "New document" }).ClickAsync();
        await Expect(Workspace.Dialog).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Control+K");

        // Nothing to wait for: what must not happen is a second dialog, so give it the time it would take.
        await Task.Delay(TimeSpan.FromSeconds(1));
        await Expect(Search.Dialog).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Ctrl_K_WhileNobodyIsSignedIn_OpensNothing()
    {
        await Page.GotoAsync(BaseAddress);
        await Expect(Workspace.LandingHeading).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Control+K");

        await Task.Delay(TimeSpan.FromSeconds(1));
        await Expect(Search.Dialog).ToHaveCountAsync(0);
    }

    // ---- language ----

    [Test]
    public async Task InGerman_TheControlAndTheDialogAreNamedInGerman_AndTheShortcutIsStrgK()
    {
        await Workspace.OpenNewUsersWorkspaceAsync(BaseAddress, $"Xaver {Guid.NewGuid():n}");
        await Context.AddCookiesAsync([
            new()
            {
                Name = ".AspNetCore.Culture",
                Value = "c=de|uic=de",
                Url = BaseAddress,
            },
        ]);
        await Page.ReloadAsync();

        var control = Page.Locator(".rz-header").GetByRole(AriaRole.Button, new() { Name = "Dokumente durchsuchen" });
        await Expect(control).ToBeVisibleAsync();
        await Expect(control).ToContainTextAsync("Strg+K");

        for (var attempt = 0; attempt < 15; attempt++)
        {
            var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Dokumente durchsuchen" });

            if (await dialog.IsVisibleAsync())
            {
                break;
            }

            try
            {
                await control.ClickAsync(new() { Timeout = 2000 });
                await Expect(dialog).ToBeVisibleAsync(new() { Timeout = 2000 });

                break;
            }
            catch (PlaywrightException) { }
        }

        await Expect(Page.GetByRole(AriaRole.Textbox, new() { Name = "Dokumente durchsuchen" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Gib mindestens 3 Zeichen ein")).ToBeVisibleAsync();
    }
}
