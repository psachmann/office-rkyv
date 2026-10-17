using DotNetQuery.Core;
using DotNetQuery.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platforms.Linux.Gtk4.BlazorWebView;
using Microsoft.Maui.Platforms.Linux.Gtk4.Hosting;
using OfficeRkyv.Desktop.Auth;
using OfficeRkyv.Desktop.Files;
using OfficeRkyv.Desktop.Hosting;
using OfficeRkyv.UI;
using OfficeRkyv.UI.Auth;
using OfficeRkyv.UI.Files;
using OfficeRkyv.UI.Settings;

namespace OfficeRkyv.Desktop;

public static class MauiProgram
{
    internal static MauiApp CreateMauiApp(DesktopHost host)
    {
        var builder = MauiApp.CreateBuilder().UseMauiAppLinuxGtk4<App>();

        builder
            .Logging.AddSimpleConsole(options => options.SingleLine = true)
            .SetMinimumLevel(DesktopHostEnvironment.IsDevelopmentEnvironment ? LogLevel.Information : LogLevel.Warning);

        builder.Services.AddBlazorWebView();
        builder.Services.AddLinuxGtk4BlazorWebView();
        builder.ConfigureMauiHandlers(handlers =>
            // The GTK4 backend's handler; its name is also MAUI's own.
            handlers.AddHandler<BlazorWebView, Microsoft.Maui.Platforms.Linux.Gtk4.BlazorWebView.BlazorWebViewHandler>()
        );

        if (DesktopHostEnvironment.IsDevelopmentEnvironment)
        {
            builder.Services.AddBlazorWebViewDeveloperTools();
        }

        // The shared UI: Radzen, localization from its resources, and the facades.
        builder.Services.AddOfficeRkyvUi();
        builder.Services.AddDotNetQuery(options =>
        {
            options.ExecutionMode = QueryExecutionMode.Ssr; // one scope per WebView, like a circuit
            options.StaleTime = TimeSpan.Zero; // data is immediately stale
            options.CacheTime = TimeSpan.FromMinutes(10); // cache entries live 10 minutes after last subscriber
            options.RefetchInterval = TimeSpan.FromSeconds(30); // automatically refetch every 30 seconds
        });

        // What this host supplies: the address of the API (its socket, not a service-discovery name) and the key its
        // calls are signed with, who is signed in, the settings, and the environment the shared layout asks for.
        DesktopApi.AddSocketClients(builder.Services, host.Paths.SocketPath, host.CallerTokenKey);
        builder.Services.AddSingleton<AuthenticationStateProvider>(host.SignIn);
        builder.Services.AddSingleton(host.SignIn);
        builder.Services.AddScoped<ISignInService, DesktopSignInService>();
        builder.Services.AddSingleton<IUserSettingsService>(host.Settings);
        builder.Services.AddSingleton<IFileSaver>(new DesktopFileSaver(host.Logger));
        builder.Services.AddSingleton<IHostEnvironment, DesktopHostEnvironment>();

        return builder.Build();
    }
}
