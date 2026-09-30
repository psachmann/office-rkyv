using System.Runtime.Versioning;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UnitTests.Desktop;

[SupportedOSPlatform("linux")]
public sealed class GtkAppearanceTests
{
    [Test]
    [Arguments(AppearancePreference.System, false, false)]
    [Arguments(AppearancePreference.System, true, true)]
    [Arguments(AppearancePreference.Light, false, false)]
    [Arguments(AppearancePreference.Light, true, false)]
    [Arguments(AppearancePreference.Dark, false, true)]
    [Arguments(AppearancePreference.Dark, true, true)]
    public async Task System_MirrorsTheDesktop_AndLightAndDarkAreFixed(
        AppearancePreference choice,
        bool desktopPrefersDark,
        bool expected
    )
    {
        await Assert.That(OfficeRkyv.Desktop.GtkAppearance.PrefersDark(choice, desktopPrefersDark)).IsEqualTo(expected);
    }
}
