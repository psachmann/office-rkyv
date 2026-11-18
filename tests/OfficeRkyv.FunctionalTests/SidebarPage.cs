using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The navigation sidebar as a person uses it: the inbox and the archives while a document page is open, the signed-in
/// user's name and sign-out, and the action that creates an archive.
/// </summary>
public sealed class SidebarPage(IPage page)
{
    public ILocator Sidebar => page.Locator(".rz-sidebar");

    /// <summary>The user's name in the sidebar, which is there only while someone is signed in.</summary>
    public ILocator SignedInName => Sidebar.GetByTestId("sidebar-user");

    public ILocator SignOutButton => Sidebar.GetByRole(AriaRole.Button, new() { Name = "Sign out" });

    /// <summary>The sidebar link that holds the inbox entry, so it carries the link's own state and click.</summary>
    public ILocator InboxEntry => SidebarLink(page.GetByTestId("nav-inbox"));

    public ILocator InboxCount => page.GetByTestId("nav-inbox-count");

    public ILocator ArchiveEntry(string name) =>
        SidebarLink(page.GetByTestId("nav-archive").Filter(new() { HasText = name }));

    public ILocator ArchiveEntries => SidebarLink(page.GetByTestId("nav-archive"));

    /// <summary>Every link in the sidebar that leads into the inbox or an archive.</summary>
    public ILocator NavigationEntries =>
        SidebarLink(page.Locator("[data-testid=nav-inbox], [data-testid=nav-archive]"));

    public async Task CreateArchiveAsync(string name)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "New archive" }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByLabel("Name").FillAsync(name);
        await page.GetByRole(AriaRole.Dialog)
            .GetByRole(AriaRole.Button, new() { Name = "Create archive" })
            .ClickAsync();
        await Expect(page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
    }

    private ILocator SidebarLink(ILocator inside) => Sidebar.GetByRole(AriaRole.Link).Filter(new() { Has = inside });
}
