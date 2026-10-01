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

    public ILocator DocumentRow(string title) => page.GetByTestId("document-row").Filter(new() { HasText = title });

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

    public async Task CreateDocumentAsync(string title)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "New document" }).ClickAsync();
        await Dialog.GetByLabel("Title").FillAsync(title);
        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Create document" }).ClickAsync();
        await Expect(Dialog).ToBeHiddenAsync();
    }

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
