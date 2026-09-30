using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platforms.Linux.Gtk4.Platform;

namespace OfficeRkyv.Desktop;

public class Program : GtkMauiApplication
{
    private MauiApp? mauiApp;

    protected override MauiApp CreateMauiApp()
    {
        // The backend runs this on every activation, not only the first: launching the app again hands over to
        // the running instance (GApplication is unique per `ApplicationId`), which activates, and so starts up,
        // once more. The API starts once, and all activations share one MauiApp: with a second one, a second
        // `Application` would own the new window and be the one signals and the settings reach, leaving the
        // first window behind when the app is told to quit.
        DesktopHost.Current.EnsureApiStarted();

        return mauiApp ??= MauiProgram.CreateMauiApp(DesktopHost.Current);
    }

    public static void Main(string[] args)
    {
        DesktopHost.Initialize();

        try
        {
            using var signals = SignalHandling.Register(DesktopHost.Current.Logger);

            new Program().Run(args);
        }
        finally
        {
            DesktopHost.Current.ShutdownAsync().GetAwaiter().GetResult();
        }
    }
}
