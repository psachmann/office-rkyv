using System.Reactive;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using OfficeRkyv.UI.Settings;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.Desktop;

/// <summary>
/// Makes the WebKit view follow the appearance choice. WebKitGTK derives the view's <c>prefers-color-scheme</c>
/// only from <c>gtk-application-prefer-dark-theme</c>, so that one setting is all there is to drive: System
/// mirrors the desktop's colour scheme into it (which desktops like COSMIC publish only through the XDG portal,
/// read by GTK as <c>gtk-interface-color-scheme</c>), and Light or Dark fix it. The host page's theme links
/// and default background are keyed on <c>prefers-color-scheme</c>, so nothing else has to change.
/// </summary>
/// <remarks>
/// The MAUI backend writes the same setting from its own theme (always light) while it builds the app and each
/// window, so the setting is also watched and put back whenever it differs from what the choice calls for.
/// </remarks>
internal static class GtkAppearance
{
    private const string DesktopColorScheme = "gtk-interface-color-scheme";
    private const string PreferDarkTheme = "gtk-application-prefer-dark-theme";

    /// <summary>
    /// Starts following the settings for as long as the application runs. Needs GTK to be initialized, and sets
    /// the preference before returning, so the window that is created next is right from its first frame.
    /// </summary>
    public static IDisposable Start(IObservable<UserSettings> settings, ILogger logger)
    {
        var gtk = Gtk.Settings.GetDefault() ?? throw new InvalidOperationException("GTK has no default settings.");

        var notifications = Observable.FromEvent<
            GObject.SignalHandler<GObject.Object, GObject.Object.NotifySignalArgs>,
            GObject.Object.NotifySignalArgs
        >(emit => (_, args) => emit(args), handler => gtk.OnNotify += handler, handler => gtk.OnNotify -= handler);

        var desktopPrefersDark = notifications
            .Where(args => args.Pspec.GetName() == DesktopColorScheme)
            .Select(_ => Unit.Default)
            .StartWith(Unit.Default)
            .Select(_ => gtk.GtkInterfaceColorScheme == Gtk.InterfaceColorScheme.Dark);

        var wanted = settings
            .Select(current => current.Appearance)
            .CombineLatest(desktopPrefersDark, PrefersDark)
            .DistinctUntilChanged();

        // Somebody else (the backend) wrote the setting: look again at what it should be.
        var overwritten = notifications
            .Where(args => args.Pspec.GetName() == PreferDarkTheme)
            .Select(_ => Unit.Default);

        return wanted
            .CombineLatest(overwritten.StartWith(Unit.Default), (preferDark, _) => preferDark)
            .Subscribe(preferDark =>
            {
                if (gtk.GtkApplicationPreferDarkTheme == preferDark)
                {
                    return;
                }

                gtk.GtkApplicationPreferDarkTheme = preferDark;
                logger.LogInformation("Appearance: the view prefers {Scheme}", preferDark ? "dark" : "light");
            });
    }

    internal static bool PrefersDark(AppearancePreference choice, bool desktopPrefersDark) =>
        choice switch
        {
            AppearancePreference.Light => false,
            AppearancePreference.Dark => true,
            _ => desktopPrefersDark,
        };
}
