using System.Globalization;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using OfficeRkyv.UI.Localization;
using OfficeRkyv.UI.Settings;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.Desktop.Settings;

/// <summary>
/// The desktop app's settings: one per process, kept in the user's settings file. A change is saved, put in
/// force where this service can do that (the language, through the process's culture) and emitted. What the
/// appearance does to the window is done through GTK by <see cref="GtkAppearance"/>, and what a new language does
/// to the UI by the root component, both by watching <see cref="Settings"/>.
/// </summary>
public sealed partial class DesktopUserSettingsService : IUserSettingsService, IDisposable
{
    private readonly BehaviorSubject<UserSettings> settings;
    private readonly DesktopSettingsStore store;
    private readonly ILogger logger;
    private int disposed;

    public DesktopUserSettingsService(DesktopSettingsStore store, UserSettings initial, ILogger logger)
    {
        this.store = store;
        this.logger = logger;
        settings = new BehaviorSubject<UserSettings>(initial);
    }

    public UserSettings Current => settings.Value;

    public IObservable<UserSettings> Settings => settings;

    public Task SetAppearanceAsync(AppearancePreference appearance) =>
        ChangeAsync(Current with { Appearance = appearance });

    public Task SetCultureAsync(CultureInfo culture) =>
        ChangeAsync(Current with { Culture = SupportedCultures.Match(culture) });

    private Task ChangeAsync(UserSettings next)
    {
        if (next == Current)
        {
            return Task.CompletedTask;
        }

        // A file that cannot be written (a read-only home, a full disk) costs the choice at the next start, not
        // the choice now.
        try
        {
            store.Save(next);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogSettingsNotSaved(logger, exception);
        }

        if (!next.Culture.Equals(Current.Culture))
        {
            // On the UI thread, where the change is made from. Before the change is emitted, so that whatever
            // renders in answer to it already reads the new culture.
            CultureApplier.Apply(next.Culture);
        }

        settings.OnNext(next);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }

        settings.OnCompleted();
        settings.Dispose();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The settings could not be saved; they apply until the app closes."
    )]
    private static partial void LogSettingsNotSaved(ILogger logger, Exception exception);
}
