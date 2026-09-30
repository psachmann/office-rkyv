using System.Globalization;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UI.Settings;

/// <summary>
/// The user's appearance and language settings: where they are kept, what is in force, and how a change
/// takes effect. The hosts differ in all three (cookies and a page reload on the web; a file and in-place
/// changes on the desktop), so the pages depend only on this.
/// </summary>
public interface IUserSettingsService
{
    /// <summary>The settings in force. Read synchronously, so a page renders from it immediately.</summary>
    UserSettings Current { get; }

    /// <summary>
    /// Emits <see cref="Current"/> to every subscriber as it subscribes, then each time the settings
    /// actually change; setting a value that is already in force emits nothing. An emission may arrive
    /// on any thread, so a component marshals to its renderer before it touches its state.
    /// </summary>
    IObservable<UserSettings> Settings { get; }

    Task SetAppearanceAsync(AppearancePreference appearance);

    /// <summary>Changes the UI language. On the web this reloads the page; on the desktop it applies in place.</summary>
    Task SetCultureAsync(CultureInfo culture);
}
