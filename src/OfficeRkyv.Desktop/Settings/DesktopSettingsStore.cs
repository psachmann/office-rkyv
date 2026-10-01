using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeRkyv.UI.Localization;
using OfficeRkyv.UI.Settings;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.Desktop.Settings;

/// <summary>
/// The user's settings file in the config directory: <c>{ "appearance": "system|light|dark", "culture": "en" }</c>.
/// Whatever is in it, the app starts: a missing, unreadable or invalid file is the same as no choice made.
/// </summary>
public sealed partial class DesktopSettingsStore(string path, ILogger? logger = null)
{
    private readonly ILogger logger = logger ?? NullLogger.Instance;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// The settings to start with. Each field falls back on its own, so one bad value does not cost the other:
    /// an appearance that is missing or unknown is System, and a culture that is missing or not supported is
    /// the closest match to the operating system's own language (<c>en-US</c> to <c>en</c>).
    /// </summary>
    /// <param name="operatingSystemCulture">The UI culture the operating system reports, which .NET derives from <c>LANG</c>.</param>
    public UserSettings Load(CultureInfo operatingSystemCulture)
    {
        var stored = Read();
        var culture =
            stored?.Culture is { } name && SupportedCultures.IsSupported(name)
                ? SupportedCultures.Match(CultureInfo.GetCultureInfo(name))
                : SupportedCultures.Match(operatingSystemCulture);

        return new UserSettings(Appearance.Parse(stored?.Appearance), culture);
    }

    /// <summary>Writes the file whole or not at all, so a crash cannot leave half a file behind.</summary>
    public void Save(UserSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporary = path + ".tmp";
        var stored = new StoredSettings(Appearance.ToCookieValue(settings.Appearance), settings.Culture.Name);
        File.WriteAllText(temporary, JsonSerializer.Serialize(stored, Json));
        File.Move(temporary, path, overwrite: true);
    }

    private StoredSettings? Read()
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LogSettingsFileUnreadable(logger, exception, path);

            return null;
        }
    }

    private sealed record StoredSettings(string? Appearance, string? Culture);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The settings file {Path} cannot be read; starting with the defaults."
    )]
    private static partial void LogSettingsFileUnreadable(ILogger logger, Exception exception, string path);
}
