extern alias web;

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using OfficeRkyv.UI.Localization;
using WebSettingsService = web::OfficeRkyv.Web.Settings.WebUserSettingsService;

namespace OfficeRkyv.UnitTests;

/// <summary>
/// A text missing from a translation does not fail anywhere: the lookup falls back to English. These tests are the
/// only thing that notices, so they compare the compiled resources of both projects that hold UI text, for every
/// shipped culture other than the default.
/// </summary>
public sealed partial class TranslationCompletenessTests
{
    private static readonly Assembly[] Assemblies =
    [
        typeof(SupportedCultures).Assembly,
        typeof(WebSettingsService).Assembly,
    ];

    private static IEnumerable<CultureInfo> Translations =>
        SupportedCultures.All.Where(culture => culture.Name != SupportedCultures.Default.Name);

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();

    /// <summary>Every resource a component's text lives in, as its manifest base name, with the assembly that holds it.</summary>
    private static IEnumerable<(Assembly Assembly, string BaseName)> Resources() =>
        Assemblies.SelectMany(assembly =>
        {
            var prefix = $"{assembly.GetName().Name}.{SupportedCultures.ResourcesPath.Replace('/', '.')}.";

            return assembly
                .GetManifestResourceNames()
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".resources"))
                .Select(name => (assembly, name[..^".resources".Length]));
        });

    private static Dictionary<string, string>? Texts(Assembly assembly, string baseName, CultureInfo culture)
    {
        var set = new ResourceManager(baseName, assembly).GetResourceSet(
            culture,
            createIfNotExists: true,
            tryParents: false
        );

        return set?.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }

    [Test]
    public async Task TheResourcesTheTestsCompare_AreFound()
    {
        // Guards the other tests against passing on an empty list, for example after the resources path moves.
        var found = Resources().Select(resource => resource.BaseName).ToList();

        await Assert.That(found).Count().IsGreaterThanOrEqualTo(11);
        await Assert.That(Translations).IsNotEmpty();
    }

    [Test]
    public async Task EveryResource_HasATranslationFileForEveryShippedLanguage()
    {
        var missing = new List<string>();

        foreach (var (assembly, baseName) in Resources())
        {
            foreach (var culture in Translations)
            {
                if (Texts(assembly, baseName, culture) is null)
                {
                    missing.Add($"{baseName} ({culture.Name})");
                }
            }
        }

        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task EveryTranslation_HasExactlyTheTextsOfTheEnglishOriginal()
    {
        var differences = new List<string>();

        foreach (var (assembly, baseName) in Resources())
        {
            var english = Texts(assembly, baseName, CultureInfo.InvariantCulture)!;

            foreach (var culture in Translations)
            {
                var translated = Texts(assembly, baseName, culture);

                if (translated is null)
                {
                    continue;
                }

                differences.AddRange(
                    english.Keys.Except(translated.Keys).Select(key => $"{baseName} ({culture.Name}): missing {key}")
                );
                differences.AddRange(
                    translated.Keys.Except(english.Keys).Select(key => $"{baseName} ({culture.Name}): unknown {key}")
                );
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task EveryTranslation_KeepsThePlaceholdersOfTheEnglishText()
    {
        var differences = new List<string>();

        foreach (var (assembly, baseName) in Resources())
        {
            var english = Texts(assembly, baseName, CultureInfo.InvariantCulture)!;

            foreach (var culture in Translations)
            {
                var translated = Texts(assembly, baseName, culture);

                if (translated is null)
                {
                    continue;
                }

                foreach (var (key, text) in english.Where(pair => translated.ContainsKey(pair.Key)))
                {
                    var expected = Placeholder().Matches(text).Select(match => match.Value).Order();
                    var actual = Placeholder().Matches(translated[key]).Select(match => match.Value).Order();

                    if (!expected.SequenceEqual(actual))
                    {
                        differences.Add($"{baseName} ({culture.Name}): {key}");
                    }
                }
            }
        }

        await Assert.That(differences).IsEmpty();
    }
}
