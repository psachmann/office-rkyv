using Microsoft.Playwright;

namespace OfficeRkyv.FunctionalTests;

// The API's development certificate is self-signed, which no browser trusts here: every context ignores HTTPS errors.
// Setting the options replaces TUnit.Playwright's defaults, so its pinned locale and colour scheme are restated.
public static class BrowserDefaults
{
    [Before(HookType.TestDiscovery)]
    public static Task Configure(BeforeTestDiscoveryContext context)
    {
        context.Settings.PlaywrightSettings.DefaultBrowserNewContextOptions = new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            Locale = "en-US",
            ColorScheme = ColorScheme.Light,
        };

        return Task.CompletedTask;
    }
}
