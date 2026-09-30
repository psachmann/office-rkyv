namespace OfficeRkyv.UI.Shared;

/// <summary>What the visitor chose: follow the operating system, or fix the UI to light or dark.</summary>
public enum AppearancePreference
{
    System,
    Light,
    Dark,
}

/// <summary>One theme stylesheet to link, and the media query that limits when the browser applies it.</summary>
/// <param name="Theme">A Radzen theme name, which names its stylesheet.</param>
/// <param name="Media">Null applies the stylesheet always; <c>not all</c> never applies it.</param>
public sealed record ThemeStylesheet(string Theme, string? Media);

public static class Appearance
{
    public const string CookieName = "office-rkyv-appearance";

    public const string LightTheme = "material";

    public const string DarkTheme = "material-dark";

    /// <summary>
    /// The cookie is client-controlled input, so only the values this app writes are honoured; anything
    /// else - missing, unknown, tampered - is the default, <see cref="AppearancePreference.System"/>.
    /// </summary>
    public static AppearancePreference Parse(string? stored) =>
        stored switch
        {
            "light" => AppearancePreference.Light,
            "dark" => AppearancePreference.Dark,
            _ => AppearancePreference.System,
        };

    public static string ToCookieValue(AppearancePreference preference) =>
        preference switch
        {
            AppearancePreference.Light => "light",
            AppearancePreference.Dark => "dark",
            _ => "system",
        };

    /// <summary>
    /// Both themes are always linked; the media query decides which one applies. Light and Dark switch by
    /// media alone (<c>not all</c> turns a sheet off), so changing the choice never removes a link or waits for
    /// a download: both sheets are already loaded, and the page never shows unstyled content in between.
    /// System limits each sheet to the operating system's preference, so the browser picks the right one for
    /// the first paint and follows the operating system on its own, which the server cannot do because it
    /// does not know that preference.
    /// </summary>
    public static IReadOnlyList<ThemeStylesheet> Stylesheets(AppearancePreference preference) =>
        preference switch
        {
            AppearancePreference.Light => [new(LightTheme, null), new(DarkTheme, Off)],
            AppearancePreference.Dark => [new(LightTheme, Off), new(DarkTheme, null)],
            _ => [new(LightTheme, "(prefers-color-scheme: light)"), new(DarkTheme, "(prefers-color-scheme: dark)")],
        };

    private const string Off = "not all";
}
