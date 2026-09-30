using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeRkyv.Desktop.Settings;
using OfficeRkyv.UI.Settings;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UnitTests.Desktop;

[SupportedOSPlatform("linux")]
public sealed class DesktopUserSettingsServiceTests : IDisposable
{
    private static readonly UserSettings Defaults = new(AppearancePreference.System, new CultureInfo("en"));

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"ork-{Guid.NewGuid():n}"[..12]);

    private string SettingsPath => Path.Combine(directory, "config", "settings.json");

    private DesktopUserSettingsService CreateService() =>
        new(new DesktopSettingsStore(SettingsPath), Defaults, NullLogger.Instance);

    // Collects what a subscriber sees, as a component's subscription would.
    private static List<UserSettings> Observe(DesktopUserSettingsService service)
    {
        var seen = new List<UserSettings>();
        service.Settings.Subscribe(seen.Add);

        return seen;
    }

    [Test]
    public async Task ANewSubscriber_FirstReceivesTheSettingsInForce()
    {
        using var service = CreateService();
        await service.SetAppearanceAsync(AppearancePreference.Dark);

        var seen = Observe(service);

        await Assert.That(seen).Count().IsEqualTo(1);
        await Assert.That(seen[0]).IsEqualTo(service.Current);
        await Assert.That(seen[0].Appearance).IsEqualTo(AppearancePreference.Dark);
    }

    [Test]
    public async Task AnAppearanceChange_IsEmittedOnce_AndSavedToTheFile()
    {
        using var service = CreateService();
        var seen = Observe(service);

        await service.SetAppearanceAsync(AppearancePreference.Light);

        await Assert.That(seen).Count().IsEqualTo(2);
        await Assert.That(seen[1].Appearance).IsEqualTo(AppearancePreference.Light);
        await Assert.That(service.Current.Appearance).IsEqualTo(AppearancePreference.Light);
        await Assert.That(File.ReadAllText(SettingsPath)).Contains("light");
    }

    [Test]
    public async Task SettingTheCurrentValue_EmitsNothing_AndWritesNoFile()
    {
        using var service = CreateService();
        var seen = Observe(service);

        await service.SetAppearanceAsync(Defaults.Appearance);
        await service.SetCultureAsync(Defaults.Culture);

        await Assert.That(seen).Count().IsEqualTo(1);
        await Assert.That(File.Exists(SettingsPath)).IsFalse();
    }

    [Test]
    public async Task ACultureOutsideTheSupportedOnes_IsMatchedToOne_SoItIsNotAChange()
    {
        using var service = CreateService();
        var seen = Observe(service);

        // en-US matches en, which is already in force; Japanese matches nothing and falls back to the default.
        await service.SetCultureAsync(new CultureInfo("en-US"));
        await service.SetCultureAsync(new CultureInfo("ja-JP"));

        await Assert.That(seen).Count().IsEqualTo(1);
        await Assert.That(service.Current.Culture.Name).IsEqualTo("en");
    }

    [Test]
    public async Task AFileThatCannotBeWritten_DoesNotStopTheChangeFromApplying()
    {
        // The directory's place is taken by a file, so the settings file cannot be created.
        File.WriteAllText(directory, "in the way");
        using var service = CreateService();
        var seen = Observe(service);

        await service.SetAppearanceAsync(AppearancePreference.Dark);

        await Assert.That(service.Current.Appearance).IsEqualTo(AppearancePreference.Dark);
        await Assert.That(seen).Count().IsEqualTo(2);
    }

    [Test]
    public async Task DisposingTwice_IsHarmless()
    {
        var service = CreateService();
        var completions = 0;
        service.Settings.Subscribe(_ => { }, () => completions++);

        service.Dispose();

        await Assert.That(() => service.Dispose()).ThrowsNothing();
        await Assert.That(completions).IsEqualTo(1);
    }

    public void Dispose()
    {
        if (File.Exists(directory))
        {
            File.Delete(directory);
        }
        else if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
