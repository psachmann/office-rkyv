using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The settings page and the appearance it controls, against the real app. The operating system's colour scheme
/// is emulated per test, so each test decides what "System" means.
/// </summary>
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AspireFixture<Projects.OfficeRkyv_AppHost>>(Shared = SharedType.PerTestSession)]
public class SettingsTests(AspireFixture<Projects.OfficeRkyv_AppHost> fixture) : PageTest
{
    private const string AppearanceCookie = "office-rkyv-appearance";

    private SettingsPage Settings => new(Page);

    private string BaseAddress =>
        fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri
        ?? throw new InvalidOperationException("The web service has no address.");

    private Task EmulateOperatingSystemAsync(ColorScheme scheme) =>
        Page.EmulateMediaAsync(new() { ColorScheme = scheme });

    private Task StoreAppearanceAsync(string value) =>
        Context.AddCookiesAsync([
            new()
            {
                Name = AppearanceCookie,
                Value = value,
                Url = BaseAddress,
            },
        ]);

    [Test]
    public async Task Settings_IsReachableFromTheSidebar_AtTheBottom_AndMarkedAsTheCurrentPage()
    {
        await Page.GotoAsync(BaseAddress);
        await Expect(Settings.SidebarEntry("Home")).ToHaveAttributeAsync("aria-current", "page");

        await Settings.SidebarEntry("Settings").ClickAsync();

        await Expect(Settings.Heading).ToBeVisibleAsync();
        await Expect(Settings.SidebarEntry("Settings")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Settings.SidebarEntry("Home")).Not.ToHaveAttributeAsync("aria-current", "page");

        // Pinned to the bottom: below the other entries, and level with the bottom of the sidebar.
        var sidebar = await Settings.Sidebar.BoundingBoxAsync();
        var settings = await Settings.SidebarEntry("Settings").BoundingBoxAsync();
        var home = await Settings.SidebarEntry("Home").BoundingBoxAsync();
        await Assert.That(settings!.Y).IsGreaterThan(home!.Y + home.Height);
        await Assert.That(sidebar!.Y + sidebar.Height - (settings.Y + settings.Height)).IsLessThan(24);
    }

    [Test]
    public async Task TheLanguageControl_IsOnTheSettingsPage_AndNotInTheHeader()
    {
        await Page.GotoAsync(BaseAddress);
        await Expect(Settings.HeaderDropdowns).ToHaveCountAsync(0);

        await Settings.OpenAsync(BaseAddress);

        await Expect(Settings.HeaderDropdowns).ToHaveCountAsync(0);
        await Expect(Settings.LanguageControl).ToBeVisibleAsync();
        await Expect(Settings.LanguageControl).ToContainTextAsync("English");
        await Expect(Page.GetByText("Language", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChoosingALanguage_ReturnsToTheSettingsPage()
    {
        // The dropdown only offers English today, and choosing the language already in use does not change the
        // value, so the reload cannot be triggered from the page yet. What the page's choice leads to can be:
        // the culture endpoint stores the choice and sends the browser back where it came from.
        // Its own request context: the app's development certificate is self-signed, which the browser context is
        // set up to accept and a plain API request is not.
        await using var api = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var response = await api.GetAsync(
            $"{BaseAddress}culture/set?culture=en&redirectUri=%2Fsettings",
            new() { MaxRedirects = 0 }
        );

        await Assert.That(response.Status).IsEqualTo(302);
        await Assert.That(response.Headers["location"]).IsEqualTo("/settings");
        await Assert.That(response.Headers["set-cookie"]).Contains(".AspNetCore.Culture=");
    }

    [Test]
    public async Task WithNoChoice_TheUiFollowsTheOperatingSystem_IncludingALiveChange()
    {
        await EmulateOperatingSystemAsync(ColorScheme.Dark);
        await Settings.OpenAsync(BaseAddress);
        await Expect(Settings.AppearanceOption("System")).ToBeCheckedAsync();
        await Settings.WaitForThemeAsync(SettingsPage.DarkTheme);
        var dark = await Settings.PageBackgroundAsync();

        // The operating system changes while the page is open: no reload, no click.
        await EmulateOperatingSystemAsync(ColorScheme.Light);

        await Settings.WaitForThemeAsync(SettingsPage.LightTheme);
        await Assert.That(await Settings.PageBackgroundAsync()).IsNotEqualTo(dark);
    }

    [Test]
    public async Task ChoosingDark_OverridesALightOperatingSystem_AtOnce_AndSurvivesAReload()
    {
        await EmulateOperatingSystemAsync(ColorScheme.Light);
        await Settings.OpenAsync(BaseAddress);
        await Settings.WaitForThemeAsync(SettingsPage.LightTheme);
        var light = await Settings.PageBackgroundAsync();

        await Settings.ChooseAppearanceAsync("Dark");

        await Settings.WaitForThemeAsync(SettingsPage.DarkTheme);
        await Assert.That(await Settings.PageBackgroundAsync()).IsNotEqualTo(light);

        // Rendered dark by the server from the first paint, and the control shows the choice.
        await Page.ReloadAsync();
        await Expect(Settings.Heading).ToBeVisibleAsync();
        await Settings.WaitForThemeAsync(SettingsPage.DarkTheme);
        await Expect(Settings.AppearanceOption("Dark")).ToBeCheckedAsync();
    }

    [Test]
    public async Task ChoosingLight_OverridesADarkOperatingSystem_AndSurvivesAReload()
    {
        await EmulateOperatingSystemAsync(ColorScheme.Dark);
        await Settings.OpenAsync(BaseAddress);

        await Settings.ChooseAppearanceAsync("Light");

        await Settings.WaitForThemeAsync(SettingsPage.LightTheme);
        await Page.ReloadAsync();
        await Expect(Settings.Heading).ToBeVisibleAsync();
        await Settings.WaitForThemeAsync(SettingsPage.LightTheme);
        await Expect(Settings.AppearanceOption("Light")).ToBeCheckedAsync();
    }

    [Test]
    public async Task ReturningToSystem_FollowsTheOperatingSystemAgain_AfterAReload()
    {
        await EmulateOperatingSystemAsync(ColorScheme.Light);
        await Settings.OpenAsync(BaseAddress);
        await Settings.ChooseAppearanceAsync("Dark");
        await Settings.WaitForThemeAsync(SettingsPage.DarkTheme);

        await Settings.ChooseAppearanceAsync("System");
        await Settings.WaitForThemeAsync(SettingsPage.LightTheme);
        await Page.ReloadAsync();
        await Expect(Settings.Heading).ToBeVisibleAsync();

        await Expect(Settings.AppearanceOption("System")).ToBeCheckedAsync();
        await Settings.WaitForThemeAsync(SettingsPage.LightTheme);
        await EmulateOperatingSystemAsync(ColorScheme.Dark);
        await Settings.WaitForThemeAsync(SettingsPage.DarkTheme);
    }

    [Test]
    public async Task ChoosingDark_ThenReachingSettingsByTheSidebar_StillShowsTheChoice()
    {
        // Settings reached by in-app navigation is not prerendered, so it must get the choice from the circuit.
        await StoreAppearanceAsync("dark");
        await Page.GotoAsync(BaseAddress);
        await Settings.WaitForThemeAsync(SettingsPage.DarkTheme);

        await Settings.SidebarEntry("Settings").ClickAsync();

        await Expect(Settings.AppearanceOption("Dark")).ToBeCheckedAsync();
    }

    [Test]
    public async Task ATamperedAppearanceCookie_IsTreatedAsSystem()
    {
        await StoreAppearanceAsync("<script>not-a-choice</script>");
        await EmulateOperatingSystemAsync(ColorScheme.Dark);

        await Settings.OpenAsync(BaseAddress);

        await Expect(Settings.AppearanceOption("System")).ToBeCheckedAsync();
        await Settings.WaitForThemeAsync(SettingsPage.DarkTheme);
    }
}
