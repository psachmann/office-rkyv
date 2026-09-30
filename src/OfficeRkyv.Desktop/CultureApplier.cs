using System.Globalization;

namespace OfficeRkyv.Desktop;

internal static class CultureApplier
{
    /// <summary>
    /// Puts a culture in force for the whole process: the threads started from now on, and the calling one. All
    /// UI work runs on the GTK main thread (observed in the spike), and <c>IStringLocalizer</c>, Radzen's own
    /// text included, reads the thread's culture when it renders.
    /// </summary>
    public static void Apply(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
