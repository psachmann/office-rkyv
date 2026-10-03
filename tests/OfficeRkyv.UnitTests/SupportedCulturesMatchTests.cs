using System.Globalization;
using OfficeRkyv.UI.Localization;

namespace OfficeRkyv.UnitTests;

public class SupportedCulturesMatchTests
{
    [Test]
    [Arguments("en", "en")]
    [Arguments("EN", "en")]
    [Arguments("en-US", "en")]
    [Arguments("en-GB", "en")]
    [Arguments("de", "de")]
    [Arguments("DE", "de")]
    [Arguments("de-DE", "de")]
    [Arguments("de-AT", "de")]
    [Arguments("de-CH", "de")]
    public async Task ASupportedCultureOrItsRegionalVariant_MatchesTheSupportedCulture(
        string requested,
        string expected
    )
    {
        var matched = SupportedCultures.Match(CultureInfo.GetCultureInfo(requested));

        await Assert.That(matched.Name).IsEqualTo(expected);
    }

    [Test]
    [Arguments("fr-FR")]
    [Arguments("fr")]
    [Arguments("ja-JP")]
    public async Task AnUnsupportedLanguage_MatchesTheDefault(string requested)
    {
        var matched = SupportedCultures.Match(CultureInfo.GetCultureInfo(requested));

        await Assert.That(matched).IsEqualTo(SupportedCultures.Default);
    }

    [Test]
    public async Task TheInvariantCulture_MatchesTheDefault()
    {
        // The invariant culture is what a process with no usable locale (LANG=C) reports.
        var matched = SupportedCultures.Match(CultureInfo.InvariantCulture);

        await Assert.That(matched).IsEqualTo(SupportedCultures.Default);
    }

    [Test]
    public async Task WithSeveralCultures_ASpecificOneWinsOverItsParent_AndAParentIsFoundThroughTheChain()
    {
        var supported = new[] { new CultureInfo("en"), new CultureInfo("de"), new CultureInfo("de-CH") };

        await Assert.That(SupportedCultures.Match(new CultureInfo("de-CH"), supported).Name).IsEqualTo("de-CH");
        await Assert.That(SupportedCultures.Match(new CultureInfo("de-AT"), supported).Name).IsEqualTo("de");
        await Assert.That(SupportedCultures.Match(new CultureInfo("de-DE"), supported).Name).IsEqualTo("de");
    }

    [Test]
    public async Task AParentIsNeverMatchedToAMoreSpecificSupportedCulture()
    {
        // "de" is not "de-CH": matching only walks up, never down, so the first supported culture answers.
        var supported = new[] { new CultureInfo("en"), new CultureInfo("de-CH") };

        await Assert.That(SupportedCultures.Match(new CultureInfo("de"), supported).Name).IsEqualTo("en");
    }

    [Test]
    public async Task ACultureWithAScriptAndRegion_WalksUpThroughEveryLevel()
    {
        var supported = new[] { new CultureInfo("en"), new CultureInfo("sr") };

        await Assert.That(SupportedCultures.Match(new CultureInfo("sr-Latn-RS"), supported).Name).IsEqualTo("sr");
    }
}
