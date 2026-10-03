using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The settings page and the sidebar entry that leads to it, as a person uses them. Which appearance is showing
/// is read from the theme stylesheets the browser applies, so it does not depend on Radzen's colours.
/// </summary>
public sealed class SettingsPage(IPage page)
{
    public const string LightTheme = "material";

    public const string DarkTheme = "material-dark";

    public ILocator Heading => page.GetByTestId("settings-heading");

    public ILocator LanguageControl => page.GetByTestId("language");

    /// <summary>Any dropdown in the header; the language control used to live there.</summary>
    public ILocator HeaderDropdowns => page.Locator(".rz-header .rz-dropdown");

    /// <summary>One entry of the language dropdown's list, by the language's own name ("Deutsch").</summary>
    public ILocator LanguageOption(string name) => page.Locator(".rz-dropdown-item").Filter(new() { HasText = name });

    public ILocator Sidebar => page.Locator(".rz-sidebar");

    public ILocator SidebarEntry(string name) => Sidebar.GetByRole(AriaRole.Link, new() { Name = name, Exact = true });

    public ILocator AppearanceOption(string name) =>
        page.GetByTestId("appearance").GetByRole(AriaRole.Radio, new() { Name = name, Exact = true });

    public async Task OpenAsync(string baseAddress)
    {
        await page.GotoAsync($"{baseAddress}settings");
        await Expect(Heading).ToBeVisibleAsync();
    }

    /// <summary>
    /// The page is rendered on the server first and only becomes interactive once its circuit connects, and a click
    /// before that does nothing. The choice shows as checked only once the circuit has handled it, so the click is
    /// repeated until it does: clicking a choice that is already made is harmless.
    /// </summary>
    public async Task ChooseAppearanceAsync(string name)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await AppearanceOption(name).ClickAsync();

            try
            {
                await Expect(AppearanceOption(name)).ToBeCheckedAsync(new() { Timeout = 2000 });

                return;
            }
            catch (PlaywrightException)
            {
                // Not interactive yet: click again.
            }
        }

        throw new TimeoutException($"Choosing the appearance \"{name}\" never took effect.");
    }

    /// <summary>
    /// Opens the language list and picks <paramref name="name"/>, which reloads the page in that language. Before
    /// the circuit connects the list does not open, so the attempt is repeated until the page's heading shows the
    /// text it has in the new language.
    /// </summary>
    public async Task ChooseLanguageAsync(string name, string headingInThatLanguage)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                await LanguageControl.ClickAsync();
                await LanguageOption(name).ClickAsync(new() { Timeout = 2000 });
                await Expect(Heading).ToHaveTextAsync(headingInThatLanguage, new() { Timeout = 5000 });

                return;
            }
            catch (PlaywrightException)
            {
                // Not interactive yet, or the list was left open: close it and try again.
                await page.Keyboard.PressAsync("Escape");
            }
        }

        throw new TimeoutException($"Choosing the language \"{name}\" never reloaded the page.");
    }

    /// <summary>
    /// Waits until exactly one theme applies to the page, and it is <paramref name="theme"/>. A theme applies
    /// when its stylesheet link is linked and its media query matches - so this follows both the choice made on
    /// the settings page and the operating system's preference.
    /// </summary>
    public async Task WaitForThemeAsync(string theme) =>
        await page.WaitForFunctionAsync(
            """
            (theme) => {
                const applied = [...document.querySelectorAll('link[data-theme]')]
                    .filter(link => matchMedia(link.media || 'all').matches)
                    .map(link => link.dataset.theme);
                return applied.length === 1 && applied[0] === theme;
            }
            """,
            theme
        );

    public async Task<string> PageBackgroundAsync() =>
        await page.Locator("body").EvaluateAsync<string>("body => getComputedStyle(body).backgroundColor");
}
