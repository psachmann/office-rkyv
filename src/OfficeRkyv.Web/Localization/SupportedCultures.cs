using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace OfficeRkyv.Web.Localization;

/// <summary>
/// Single source of truth for which UI cultures this app ships resources for.
/// Used both for ASP.NET Core's request localization pipeline and the header language switcher,
/// so the two can never disagree about what is actually supported.
/// </summary>
public static class SupportedCultures
{
    public static readonly string ResourcesPath = "Resources";

    public static readonly CultureInfo[] All = [new CultureInfo("en")];

    public static readonly CultureInfo Default = All[0];

    public static bool IsSupported(string cultureName) =>
        All.Any(culture => culture.Name.Equals(cultureName, StringComparison.OrdinalIgnoreCase));

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
