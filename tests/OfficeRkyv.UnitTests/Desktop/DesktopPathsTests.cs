using System.Runtime.Versioning;
using OfficeRkyv.Desktop.Hosting;

namespace OfficeRkyv.UnitTests.Desktop;

[SupportedOSPlatform("linux")]
public class DesktopPathsTests
{
    private static Func<string, string?> Environment(params (string Name, string? Value)[] variables) =>
        name => variables.FirstOrDefault(variable => variable.Name == name).Value;

    [Test]
    public async Task WithNoXdgVariables_DataAndConfigLiveUnderTheHomeDirectory()
    {
        var paths = DesktopPaths.Resolve(Environment(), "/home/ada");

        await Assert.That(paths.DataDirectory).IsEqualTo("/home/ada/.local/share/office-rkyv");
        await Assert.That(paths.ConfigDirectory).IsEqualTo("/home/ada/.config/office-rkyv");
        await Assert.That(paths.DatabasePath).IsEqualTo("/home/ada/.local/share/office-rkyv/office-rkyv.db");
        await Assert.That(paths.SettingsPath).IsEqualTo("/home/ada/.config/office-rkyv/settings.json");
    }

    [Test]
    public async Task XdgVariables_AreUsedWhenTheyAreAbsolutePaths()
    {
        var paths = DesktopPaths.Resolve(
            Environment(
                ("XDG_DATA_HOME", "/srv/data"),
                ("XDG_CONFIG_HOME", "/srv/config"),
                ("XDG_RUNTIME_DIR", "/run/user/1000")
            ),
            "/home/ada"
        );

        await Assert.That(paths.DatabasePath).IsEqualTo("/srv/data/office-rkyv/office-rkyv.db");
        await Assert.That(paths.SettingsPath).IsEqualTo("/srv/config/office-rkyv/settings.json");
        await Assert.That(paths.SocketPath).IsEqualTo("/run/user/1000/office-rkyv/api.sock");
    }

    [Test]
    [Arguments("")]
    [Arguments("relative/path")]
    [Arguments("~/data")]
    public async Task AnEmptyOrNonAbsoluteXdgVariable_IsIgnored(string value)
    {
        var paths = DesktopPaths.Resolve(
            Environment(("XDG_DATA_HOME", value), ("XDG_CONFIG_HOME", value)),
            "/home/ada"
        );

        await Assert.That(paths.DataDirectory).IsEqualTo("/home/ada/.local/share/office-rkyv");
        await Assert.That(paths.ConfigDirectory).IsEqualTo("/home/ada/.config/office-rkyv");
    }

    [Test]
    public async Task WithoutARuntimeDirectory_ThePrivateFallbackIsUnderTheTempPath()
    {
        var paths = DesktopPaths.Resolve(Environment(), "/home/ada");

        await Assert.That(paths.RuntimeDirectory).StartsWith(Path.GetTempPath());
        await Assert.That(paths.SocketPath).EndsWith("api.sock");
    }
}
