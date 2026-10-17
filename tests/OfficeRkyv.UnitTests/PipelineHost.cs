using System.Net;
using Fisher;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeRkyv.Api;
using OfficeRkyv.Api.Shared.Blobs;
using Wolverine;
using Wolverine.Tracking;

namespace OfficeRkyv.UnitTests;

/// <summary>
/// The real Wolverine pipeline - validation and decider middleware, concurrency guard and all - over a
/// real Fisher store in a temp SQLite file, with every gRPC service served through the real error
/// mapping, so over-the-wire tests see exactly what a client sees.
/// </summary>
public sealed class PipelineHost : IAsyncDisposable
{
    private readonly string path;

    /// <summary>
    /// Where this host keeps file content: beside the database and named after it, as the API server derives it,
    /// and deleted with it.
    /// </summary>
    public string BlobDirectory => path + ".blobs";
    private WebApplication host = null!;

    private PipelineHost(string? databasePath) =>
        path = databasePath ?? Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.db");

    private AsyncServiceScope scope;

    /// <summary>
    /// Resolved through a scope that lives as long as the host: the bus is a scoped service, which the root
    /// provider refuses in the Development environment, where scopes are validated.
    /// </summary>
    public IMessageBus Bus => scope.ServiceProvider.GetRequiredService<IMessageBus>();

    public IDocumentStore Store => host.Services.GetRequiredService<IDocumentStore>();

    public IHost Host => host;

    /// <summary>
    /// Invokes the message and waits until everything it cascaded has been handled too - the outbox
    /// deliveries included - so a test can assert on the settled state.
    /// </summary>
    public Task<ITrackedSession> InvokeAndSettleAsync(object message) => host.InvokeMessageAndWaitAsync(message);

    public GrpcChannel Channel { get; private set; } = null!;

    public Uri Address { get; private set; } = null!;

    /// <param name="environmentName">The host environment; the default is what the test run itself reports.</param>
    /// <param name="configure">Runs after the API composition, for a host-specific addition such as the seed.</param>
    /// <param name="maxFileSizeBytes">The largest accepted file; a test that needs a limit it can reach sets it.</param>
    /// <param name="databasePath">
    /// An existing database file to start on, with the blob directory beside it, instead of a new one: a test that
    /// needs data written by an earlier version, or by an earlier host. The host deletes both when it is disposed.
    /// </param>
    public static async Task<PipelineHost> StartAsync(
        string? environmentName = null,
        Action<WebApplicationBuilder>? configure = null,
        long? maxFileSizeBytes = null,
        string? databasePath = null
    )
    {
        var pipeline = new PipelineHost(databasePath);
        await pipeline.StartCoreAsync(environmentName, configure, maxFileSizeBytes);

        return pipeline;
    }

    private async Task StartCoreAsync(
        string? environmentName,
        Action<WebApplicationBuilder>? configure,
        long? maxFileSizeBytes
    )
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
        builder.WebHost.UseKestrel(kestrel =>
            kestrel.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2)
        );
        builder.AddOfficeRkyvApi($"Data Source={path}", BlobDirectory);

        if (maxFileSizeBytes is { } limit)
        {
            builder.Services.Configure<FileStorageOptions>(options => options.MaxFileSizeBytes = limit);
        }

        configure?.Invoke(builder);

        host = builder.Build();
        host.MapOfficeRkyvApi();
        await host.StartAsync();
        scope = host.Services.CreateAsyncScope();

        var addresses = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        Address = new Uri(addresses.Addresses.Single());
        Channel = GrpcChannel.ForAddress(Address);
    }

    public async ValueTask DisposeAsync()
    {
        await scope.DisposeAsync();
        Channel.Dispose();
        await host.StopAsync();
        await host.DisposeAsync();
        File.Delete(path);

        if (Directory.Exists(BlobDirectory))
        {
            Directory.Delete(BlobDirectory, recursive: true);
        }
    }
}
