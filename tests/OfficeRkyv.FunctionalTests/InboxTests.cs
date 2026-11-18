using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The inbox page against the real app: its empty state, creating documents, and the addresses that are no longer
/// pages. One AppHost (and so one SQLite database) is shared by the whole session, so every test creates its own
/// user and never looks at anyone else's data.
/// </summary>
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public class InboxTests(AppHostFixture fixture) : PageTest
{
    private SignInPage SignIn => new(Page);

    private InboxPage Inbox => new(Page);

    private SidebarPage Sidebar => new(Page);

    private string BaseAddress =>
        fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri
        ?? throw new InvalidOperationException("The web service has no address.");

    [Test]
    public async Task ANewUser_OpensAnEmptyInbox_WithNoArchives()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Alice {Guid.NewGuid():n}");

        await Expect(Sidebar.InboxEntry).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Sidebar.InboxCount).ToHaveTextAsync("0");
        await Expect(Inbox.EmptyState).ToBeVisibleAsync();
        await Expect(Sidebar.ArchiveEntries).ToHaveCountAsync(0);
    }

    [Test]
    public async Task UploadingAFile_CreatesADocumentTitledFromItsName_InTheInbox()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Ulla {Guid.NewGuid():n}");

        await Inbox.UploadAsync(new InboxPage.UploadFile("Invoice March.pdf", [1, 2, 3, 4, 5]));

        await Expect(Inbox.UploadState("Invoice March.pdf")).ToHaveTextAsync("Done");
        await Inbox.CloseUploadDialogAsync();
        await Expect(Inbox.DocumentRow("Invoice March")).ToBeVisibleAsync();
        await Expect(Sidebar.InboxCount).ToHaveTextAsync("1");
    }

    [Test]
    public async Task UploadingSeveralFiles_CreatesOneDocumentForEach()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Vera {Guid.NewGuid():n}");

        await Inbox.UploadAsync(
            new InboxPage.UploadFile("invoice.pdf", [1, 2, 3]),
            new InboxPage.UploadFile("lease.pdf", [4, 5, 6])
        );

        await Expect(Inbox.UploadState("invoice.pdf")).ToHaveTextAsync("Done");
        await Expect(Inbox.UploadState("lease.pdf")).ToHaveTextAsync("Done");
        await Inbox.CloseUploadDialogAsync();
        await Expect(Inbox.DocumentRow("invoice")).ToBeVisibleAsync();
        await Expect(Inbox.DocumentRow("lease")).ToBeVisibleAsync();
        await Expect(Sidebar.InboxCount).ToHaveTextAsync("2");
    }

    [Test]
    public async Task ARefusedFile_IsShownAsRefused_AndDoesNotStopTheOthers()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Wilma {Guid.NewGuid():n}");

        await Inbox.UploadAsync(
            new InboxPage.UploadFile("empty.txt", [], "text/plain"),
            new InboxPage.UploadFile("lease.pdf", [4, 5, 6])
        );

        await Expect(Inbox.UploadState("empty.txt")).ToHaveTextAsync("This file is empty.");
        await Expect(Inbox.UploadState("lease.pdf")).ToHaveTextAsync("Done");
        await Inbox.CloseUploadDialogAsync();
        await Expect(Inbox.DocumentRow("lease")).ToBeVisibleAsync();
        await Expect(Sidebar.InboxCount).ToHaveTextAsync("1");
    }

    [Test]
    public async Task TheDialogs_FlagMissingInput_AndShowTheServersRefusalInTheUsersLanguage()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Bob {Guid.NewGuid():n}");

        // A missing name is flagged before anything is sent; the dialog stays open.
        await Page.GetByRole(AriaRole.Button, new() { Name = "New archive" }).ClickAsync();
        await Inbox.Dialog.GetByRole(AriaRole.Button, new() { Name = "Create archive" }).ClickAsync();
        await Expect(Inbox.Dialog.GetByText("Enter a name.")).ToBeVisibleAsync();

        // Too long is only known to the server, whose refusal comes back as a localized message.
        await Inbox.Dialog.GetByLabel("Name").FillAsync(new string('n', 101));
        await Inbox.Dialog.GetByRole(AriaRole.Button, new() { Name = "Create archive" }).ClickAsync();
        await Expect(Inbox.Dialog.GetByText("This name is too long.")).ToBeVisibleAsync();

        // Correcting it in the same dialog works.
        await Inbox.Dialog.GetByLabel("Name").FillAsync("Household");
        await Inbox.Dialog.GetByRole(AriaRole.Button, new() { Name = "Create archive" }).ClickAsync();
        await Expect(Sidebar.ArchiveEntry("Household")).ToBeVisibleAsync();

        // A document needs a file: the server refuses an empty one, and the dialog says so in the user's language.
        await Inbox.UploadAsync(new InboxPage.UploadFile("empty.txt", [], "text/plain"));
        await Expect(Inbox.UploadState("empty.txt")).ToHaveTextAsync("This file is empty.");
    }

    [Test]
    public async Task TheOldWorkspaceAddresses_ShowNotFound_AndNoEntries_WhileSignedIn()
    {
        await SignIn.OpenNewUsersInboxAsync(BaseAddress, $"Judith {Guid.NewGuid():n}");

        foreach (
            var address in new[] { "workspace", $"workspace/archives/{Guid.NewGuid()}", $"users/{Guid.NewGuid()}" }
        )
        {
            await Page.GotoAsync($"{BaseAddress}{address}");

            await Expect(SignIn.NotFound).ToBeVisibleAsync();
            await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(address)}$"));
            await Expect(Sidebar.NavigationEntries).ToHaveCountAsync(0);
        }
    }

    [Test]
    public async Task TheOldWorkspaceAddress_ShowsNotFound_WhileSignedOut()
    {
        await Page.GotoAsync($"{BaseAddress}workspace");

        await Expect(SignIn.NotFound).ToBeVisibleAsync();
        await Expect(SignIn.LandingHeading).ToHaveCountAsync(0);
    }
}
