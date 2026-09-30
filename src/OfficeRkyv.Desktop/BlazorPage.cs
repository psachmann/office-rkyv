using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Maui.Controls;

namespace OfficeRkyv.Desktop;

/// <summary>The window's only content: the shared UI in a WebKitGTK view.</summary>
public class BlazorPage : ContentPage
{
    public BlazorPage()
    {
        var webView = new BlazorWebView { HostPage = "wwwroot/index.html", StartPath = "/" };
        webView.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(Components.Main) });

        if (DesktopHostEnvironment.IsDevelopmentEnvironment)
        {
            // The page's own console (and its errors) are otherwise invisible: show them on stdout.
            webView.HandlerChanged += (_, _) =>
                DevelopmentConsole.Forward(webView.Handler?.PlatformView as Gtk.Widget, DesktopHost.Current.Logger);
        }

        Content = webView;
    }
}
