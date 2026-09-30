using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UnitTests.Web;

public sealed class AppearanceTests
{
    [Test]
    [Arguments("light", AppearancePreference.Light)]
    [Arguments("dark", AppearancePreference.Dark)]
    [Arguments("system", AppearancePreference.System)]
    public async Task Parse_ReadsEachStoredChoice(string stored, AppearancePreference expected)
    {
        await Assert.That(Appearance.Parse(stored)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("Dark")]
    [Arguments("material-dark")]
    [Arguments("<script>alert(1)</script>")]
    public async Task Parse_TreatsAMissingOrUnrecognisedValueAsSystem(string? stored)
    {
        // The cookie is client-controlled, so a value the app never wrote must not pick a stylesheet.
        await Assert.That(Appearance.Parse(stored)).IsEqualTo(AppearancePreference.System);
    }

    [Test]
    [Arguments(AppearancePreference.System)]
    [Arguments(AppearancePreference.Light)]
    [Arguments(AppearancePreference.Dark)]
    public async Task ACookieValueWrittenForAChoice_ParsesBackToIt(AppearancePreference preference)
    {
        await Assert.That(Appearance.Parse(Appearance.ToCookieValue(preference))).IsEqualTo(preference);
    }

    [Test]
    public async Task Light_AppliesTheLightTheme_AndSwitchesTheDarkOneOff()
    {
        var sheets = Appearance.Stylesheets(AppearancePreference.Light);

        await Assert
            .That(sheets)
            .IsEquivalentTo([new ThemeStylesheet("material", null), new ThemeStylesheet("material-dark", "not all")]);
    }

    [Test]
    public async Task Dark_AppliesTheDarkTheme_AndSwitchesTheLightOneOff()
    {
        var sheets = Appearance.Stylesheets(AppearancePreference.Dark);

        await Assert
            .That(sheets)
            .IsEquivalentTo([new ThemeStylesheet("material", "not all"), new ThemeStylesheet("material-dark", null)]);
    }

    [Test]
    public async Task System_LinksBothThemes_EachLimitedToTheOperatingSystemsPreference()
    {
        var sheets = Appearance.Stylesheets(AppearancePreference.System);

        await Assert
            .That(sheets)
            .IsEquivalentTo([
                new ThemeStylesheet("material", "(prefers-color-scheme: light)"),
                new ThemeStylesheet("material-dark", "(prefers-color-scheme: dark)"),
            ]);
    }
}
