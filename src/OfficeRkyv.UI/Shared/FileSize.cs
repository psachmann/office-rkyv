using System.Globalization;

namespace OfficeRkyv.UI.Shared;

public static class FileSize
{
    /// <summary>
    /// A size as a person reads it: <c>812 B</c>, <c>96.0 KB</c>, <c>1.4 MB</c>. The units are symbols, so there
    /// is no text to translate; the number follows the current culture.
    /// </summary>
    public static string Format(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(CultureInfo.CurrentCulture, $"{bytes} {units[0]}")
            : string.Create(CultureInfo.CurrentCulture, $"{value:0.0} {units[unit]}");
    }
}
