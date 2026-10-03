using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace OfficeRkyv.UI.Localization;

/// <summary>
/// Single source of truth for which UI cultures this app ships resources for.
/// Used both for ASP.NET Core's request localization pipeline and the language switcher on the settings page,
/// so the two can never disagree about what is actually supported.
/// </summary>
public static class SupportedCultures
{
    public static readonly string ResourcesPath = "Localization/Resources";

    public static readonly CultureInfo[] All = [new CultureInfo("en"), new CultureInfo("de")];

    public static readonly CultureInfo Default = All[0];

    public static bool IsSupported(string cultureName) =>
        All.Any(culture => culture.Name.Equals(cultureName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The supported culture that best answers <paramref name="requested"/>: the same culture, else the nearest
    /// supported parent (<c>en-US</c> to <c>en</c>), else <see cref="Default"/>. The web host gets this from
    /// ASP.NET Core's request localization; a host without requests (the desktop app, whose language comes from
    /// the operating system) asks here.
    /// </summary>
    /// <param name="requested">The culture asked for, for example the operating system's UI culture.</param>
    /// <param name="supported">The cultures to choose from; defaults to <see cref="All"/>.</param>
    public static CultureInfo Match(CultureInfo requested, IReadOnlyList<CultureInfo>? supported = null)
    {
        supported ??= All;

        // The invariant culture (empty name) is the top of every chain and is never a supported culture.
        for (var candidate = requested; candidate.Name.Length > 0; candidate = candidate.Parent)
        {
            var match = supported.FirstOrDefault(culture =>
                culture.Name.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase)
            );

            if (match is not null)
            {
                return match;
            }
        }

        return supported.Count > 0 ? supported[0] : Default;
    }

    /// <summary>
    /// Cookie override takes precedence over the browser's Accept-Language header; falls back to
    /// <see cref="Default"/> when neither names a culture in <see cref="All"/>.
    /// </summary>
    public static RequestLocalizationOptions CreateRequestLocalizationOptions() =>
        new()
        {
            DefaultRequestCulture = new RequestCulture(Default),
            SupportedCultures = All,
            SupportedUICultures = All,
            RequestCultureProviders =
            [
                new CookieRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider(),
            ],
        };
}
