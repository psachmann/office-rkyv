using Microsoft.Extensions.Logging;

namespace OfficeRkyv.Desktop;

internal static partial class DevelopmentConsole
{
    /// <summary>
    /// Sends the WebKit view's console to stdout. The platform view is a <c>Gtk.Box</c> holding the
    /// <c>WebKit.WebView</c>, which is found by walking the widget tree.
    /// </summary>
    public static void Forward(Gtk.Widget? platformView, ILogger logger)
    {
        if (Find(platformView) is not { } webKit)
        {
            LogWebKitViewNotFound(logger);

            return;
        }

        if (webKit.GetSettings() is { } settings)
        {
            settings.EnableWriteConsoleMessagesToStdout = true;
            settings.EnableDeveloperExtras = true;
        }
    }

    private static WebKit.WebView? Find(Gtk.Widget? widget)
    {
        if (widget is WebKit.WebView webView)
        {
            return webView;
        }

        for (var child = widget?.GetFirstChild(); child is not null; child = child.GetNextSibling())
        {
            if (Find(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The WebKit view was not found, so its console is not forwarded."
    )]
    private static partial void LogWebKitViewNotFound(ILogger logger);
}
