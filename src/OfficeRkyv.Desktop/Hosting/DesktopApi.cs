using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using OfficeRkyv.Api;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;
using Wolverine.Grpc.Client;

namespace OfficeRkyv.Desktop.Hosting;

/// <summary>Another instance of the desktop app already owns the API socket.</summary>
public sealed class AlreadyRunningException(string socketPath)
    : InvalidOperationException($"Another instance is already running (it answers on {socketPath}).");

/// <summary>
/// The API inside the desktop process: the same composition as the API server, served over a unix socket in
/// the user's private runtime directory instead of a network port. Free of GTK, so it can be tested without a
/// window.
/// </summary>
public sealed class DesktopApi : IAsyncDisposable
{
    private readonly WebApplication app;

    private DesktopApi(WebApplication app, string socketPath)
    {
        this.app = app;
        SocketPath = socketPath;
    }

    public string SocketPath { get; }

    /// <summary>The addresses the API listens on; only ever the socket.</summary>
    public IReadOnlyCollection<string> Addresses =>
        [.. app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses];

    /// <exception cref="AlreadyRunningException">Another instance answers on the socket.</exception>
    public static async Task<DesktopApi> StartAsync(DesktopPaths paths, LogLevel minimumLogLevel = LogLevel.Warning)
    {
        Directory.CreateDirectory(paths.DataDirectory);
        CreatePrivateDirectory(paths.RuntimeDirectory);
        ClaimSocket(paths.SocketPath);

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory }
        );
        builder
            .Logging.ClearProviders()
            .AddSimpleConsole(options => options.SingleLine = true)
            .SetMinimumLevel(minimumLogLevel);
        builder.WebHost.UseKestrel(kestrel =>
            kestrel.ListenUnixSocket(paths.SocketPath, listen => listen.Protocols = HttpProtocols.Http2)
        );

        // The generic host's console lifetime would take SIGTERM/SIGINT and stop only the API, leaving the
        // window running with no API behind it. The desktop process owns the signals instead.
        builder.Services.AddSingleton<IHostLifetime, NoSignalLifetime>();

        builder.AddOfficeRkyvApi($"Data Source={paths.DatabasePath}");

        var app = builder.Build();
        app.MapOfficeRkyvApi();
        await app.StartAsync();

        return new DesktopApi(app, paths.SocketPath);
    }

    /// <summary>
    /// Registers the gRPC clients the UI's facades use, pointed at the socket. "http://localhost" only
    /// names the authority; the handler decides where the bytes go.
    /// </summary>
    public static IServiceCollection AddSocketClients(IServiceCollection services, string socketPath)
    {
        AddClient<UsersService.UsersServiceClient>(services, socketPath);
        AddClient<ArchivesService.ArchivesServiceClient>(services, socketPath);
        AddClient<DocumentsService.DocumentsServiceClient>(services, socketPath);

        return services;
    }

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }

    private static void AddClient<TClient>(IServiceCollection services, string socketPath)
        where TClient : class
    {
        var builder = services.AddWolverineGrpcClient<TClient>(options =>
            options.Address = new Uri("http://localhost")
        );

        // Proto-first clients, like all of these, always have an HTTP client builder to configure.
        var http =
            builder.HttpClientBuilder
            ?? throw new InvalidOperationException(
                $"The gRPC client {typeof(TClient).Name} has no HTTP client builder."
            );

        http.ConfigurePrimaryHttpMessageHandler(() =>
            new SocketsHttpHandler
            {
                ConnectCallback = async (_, cancellationToken) =>
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);

                    return new NetworkStream(socket, ownsSocket: true);
                },
            }
        );
    }

    // Readable, writable and searchable by the owner only: the socket inside is the only door to the data.
    private static void CreatePrivateDirectory(string path)
    {
        const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

        Directory.CreateDirectory(path, OwnerOnly);
        File.SetUnixFileMode(path, OwnerOnly);
    }

    /// <summary>
    /// A socket file left by a crash makes Kestrel fail with "address already in use". A live socket answers a
    /// connection, a stale one refuses it. So: it answers, another instance runs (refuse to start); it
    /// refuses, the file is stale and is removed. This is also the single-instance check.
    /// </summary>
    private static void ClaimSocket(string socketPath)
    {
        if (!File.Exists(socketPath))
        {
            return;
        }

        using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        try
        {
            probe.Connect(new UnixDomainSocketEndPoint(socketPath));
        }
        catch (SocketException)
        {
            File.Delete(socketPath);

            return;
        }

        throw new AlreadyRunningException(socketPath);
    }

    private sealed class NoSignalLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
