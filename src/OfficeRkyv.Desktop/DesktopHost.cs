using System.Globalization;
using Microsoft.Extensions.Logging;
using OfficeRkyv.Desktop.Hosting;
using OfficeRkyv.Desktop.Settings;

namespace OfficeRkyv.Desktop;

/// <summary>
/// What lives as long as the desktop process, apart from the window: where things are kept, the user's settings,
/// and the API. The API starts when the window is first created, so only the running (primary) instance ever has
/// one, and it stops when the application has ended.
/// </summary>
internal sealed class DesktopHost
{
    private DesktopApi? api;
    private IDisposable? appearance;

    private DesktopHost(DesktopPaths paths, DesktopUserSettingsService settings, ILogger logger)
    {
        Paths = paths;
        Settings = settings;
        Logger = logger;
    }

    public static DesktopHost Current { get; private set; } = null!;

    public DesktopPaths Paths { get; }

    public DesktopUserSettingsService Settings { get; }

    public ILogger Logger { get; }

    /// <summary>Reads the settings and puts the chosen language in force, before any UI exists.</summary>
    public static DesktopHost Initialize()
    {
        var logger = LoggerFactory
            .Create(logging =>
                logging
                    .AddSimpleConsole(options => options.SingleLine = true)
                    .SetMinimumLevel(
                        DesktopHostEnvironment.IsDevelopmentEnvironment ? LogLevel.Information : LogLevel.Warning
                    )
            )
            .CreateLogger("OfficeRkyv.Desktop");

        var paths = DesktopPaths.FromEnvironment();
        var store = new DesktopSettingsStore(paths.SettingsPath, logger);
        var initial = store.Load(CultureInfo.CurrentUICulture);
        CultureApplier.Apply(initial.Culture);

        logger.LogInformation(
            "Settings: {Appearance}, {Culture} (from {Path})",
            initial.Appearance,
            initial.Culture.Name,
            paths.SettingsPath
        );

        return Current = new DesktopHost(paths, new DesktopUserSettingsService(store, initial, logger), logger);
    }

    /// <summary>Starts the API once, however often the window is activated.</summary>
    public void EnsureApiStarted()
    {
        if (api is not null)
        {
            return;
        }

        api = DesktopApi.StartAsync(Paths).GetAwaiter().GetResult();
        Logger.LogInformation(
            "API listening on {Address}, data in {Database}",
            string.Join(", ", api.Addresses),
            Paths.DatabasePath
        );
    }

    /// <summary>
    /// Makes the view follow the appearance setting, once. Needs GTK to be running, and is done before the window
    /// is created (see <c>App.CreateWindow</c>) so that its first frame is right.
    /// </summary>
    public void StartAppearance() => appearance ??= GtkAppearance.Start(Settings.Settings, Logger);

    /// <summary>Stops the API, which removes its socket.</summary>
    public async Task ShutdownAsync()
    {
        if (api is { } running)
        {
            api = null;
            await running.DisposeAsync();
            Logger.LogInformation("API stopped");
        }

        appearance?.Dispose();
        Settings.Dispose();
    }
}
