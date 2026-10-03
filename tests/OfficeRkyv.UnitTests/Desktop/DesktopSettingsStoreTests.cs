using System.Globalization;
using System.Runtime.Versioning;
using OfficeRkyv.Desktop.Settings;
using OfficeRkyv.UI.Settings;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UnitTests.Desktop;

[SupportedOSPlatform("linux")]
public sealed class DesktopSettingsStoreTests : IDisposable
{
    private static readonly CultureInfo EnglishOs = new("en-US");

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"ork-{Guid.NewGuid():n}"[..12]);

    private string SettingsPath => Path.Combine(directory, "config", "settings.json");

    private DesktopSettingsStore Store => new(SettingsPath);

    private void WriteFile(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, content);
    }

    [Test]
    public async Task WithoutAFile_TheDefaultsApply_AndTheLanguageFollowsTheOperatingSystem()
    {
        var settings = Store.Load(EnglishOs);

        await Assert.That(settings.Appearance).IsEqualTo(AppearancePreference.System);
        await Assert.That(settings.Culture.Name).IsEqualTo("en");
    }

    [Test]
    public async Task AValidFile_IsHonoured()
    {
        WriteFile("""{ "appearance": "dark", "culture": "en" }""");

        var settings = Store.Load(EnglishOs);

        await Assert.That(settings.Appearance).IsEqualTo(AppearancePreference.Dark);
        await Assert.That(settings.Culture.Name).IsEqualTo("en");
    }

    [Test]
    [Arguments("{ not json")]
    [Arguments("")]
    [Arguments("[]")]
    [Arguments("null")]
    [Arguments("""{ "appearance": 5 }""")]
    [Arguments("\u0000\u0001\u0002")]
    public async Task ABrokenFile_MeansTheDefaults_AndDoesNotThrow(string content)
    {
        WriteFile(content);

        var settings = Store.Load(EnglishOs);

        await Assert.That(settings.Appearance).IsEqualTo(AppearancePreference.System);
        await Assert.That(settings.Culture.Name).IsEqualTo("en");
    }

    [Test]
    public async Task AnUnknownAppearance_FallsBackToSystem_ButTheCultureIsKept()
    {
        WriteFile("""{ "appearance": "sepia", "culture": "en" }""");

        var settings = Store.Load(EnglishOs);

        await Assert.That(settings.Appearance).IsEqualTo(AppearancePreference.System);
        await Assert.That(settings.Culture.Name).IsEqualTo("en");
    }

    [Test]
    [Arguments("fr")]
    [Arguments("xx-not-a-culture")]
    [Arguments("")]
    public async Task AStoredCultureThatIsNotSupported_FallsBackToTheOperatingSystemsLanguage_ButTheAppearanceIsKept(
        string culture
    )
    {
        WriteFile($$"""{ "appearance": "light", "culture": "{{culture}}" }""");

        var settings = Store.Load(EnglishOs);

        await Assert.That(settings.Appearance).IsEqualTo(AppearancePreference.Light);
        await Assert.That(settings.Culture.Name).IsEqualTo("en");
    }

    [Test]
    [Arguments("ja-JP")]
    [Arguments("fr-FR")]
    [Arguments("")]
    public async Task AnOperatingSystemLanguageThatIsNotSupported_ResolvesToEnglish(string osCulture)
    {
        var settings = Store.Load(CultureInfo.GetCultureInfo(osCulture));

        await Assert.That(settings.Culture.Name).IsEqualTo("en");
    }

    [Test]
    [Arguments("de-DE")]
    [Arguments("de-AT")]
    [Arguments("de")]
    public async Task AGermanOperatingSystem_ResolvesToGerman_WhenNothingIsStored(string osCulture)
    {
        var settings = Store.Load(CultureInfo.GetCultureInfo(osCulture));

        await Assert.That(settings.Culture.Name).IsEqualTo("de");
    }

    [Test]
    public async Task AStoredGermanChoice_IsKept_UnderAnEnglishOperatingSystem()
    {
        WriteFile("""{ "appearance": "dark", "culture": "de" }""");

        var settings = Store.Load(EnglishOs);

        await Assert.That(settings.Appearance).IsEqualTo(AppearancePreference.Dark);
        await Assert.That(settings.Culture.Name).IsEqualTo("de");
    }

    [Test]
    public async Task AStoredEnglishChoice_IsKept_UnderAGermanOperatingSystem()
    {
        WriteFile("""{ "appearance": "light", "culture": "en" }""");

        var settings = Store.Load(CultureInfo.GetCultureInfo("de-DE"));

        await Assert.That(settings.Culture.Name).IsEqualTo("en");
    }

    [Test]
    public async Task SavedSettings_AreReadBackUnchanged()
    {
        var saved = new UserSettings(AppearancePreference.Dark, new CultureInfo("en"));

        Store.Save(saved);
        var loaded = Store.Load(EnglishOs);

        await Assert.That(loaded).IsEqualTo(saved);
    }

    [Test]
    public async Task Saving_CreatesTheConfigDirectory_AndLeavesNoTemporaryFile()
    {
        Store.Save(new UserSettings(AppearancePreference.Light, new CultureInfo("en")));

        await Assert.That(File.Exists(SettingsPath)).IsTrue();
        await Assert.That(Directory.GetFiles(Path.GetDirectoryName(SettingsPath)!)).IsEquivalentTo([SettingsPath]);
    }

    [Test]
    public async Task Saving_ReplacesWhatWasThere()
    {
        Store.Save(new UserSettings(AppearancePreference.Dark, new CultureInfo("en")));
        Store.Save(new UserSettings(AppearancePreference.Light, new CultureInfo("en")));

        await Assert.That(Store.Load(EnglishOs).Appearance).IsEqualTo(AppearancePreference.Light);
    }

    [Test]
    public async Task TheFileIsPlainJson_WithTheDocumentedNames()
    {
        Store.Save(new UserSettings(AppearancePreference.Dark, new CultureInfo("en")));

        var text = await File.ReadAllTextAsync(SettingsPath);

        await Assert.That(text).Contains("\"appearance\": \"dark\"");
        await Assert.That(text).Contains("\"culture\": \"en\"");
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
