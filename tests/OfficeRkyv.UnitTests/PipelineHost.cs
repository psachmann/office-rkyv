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
    private readonly string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.db");
    private WebApplication host = null!;

    private PipelineHost() { }

    public IMessageBus Bus => host.Services.GetRequiredService<IMessageBus>();

    public IDocumentStore Store => host.Services.GetRequiredService<IDocumentStore>();

    public IHost Host => host;

    /// <summary>
    /// Invokes the message and waits until everything it cascaded has been handled too - the outbox
    /// deliveries included - so a test can assert on the settled state.
    /// </summary>
    public Task<ITrackedSession> InvokeAndSettleAsync(object message) => host.InvokeMessageAndWaitAsync(message);

    public GrpcChannel Channel { get; private set; } = null!;

    public Uri Address { get; private set; } = null!;

    public static async Task<PipelineHost> StartAsync()
    {
        var pipeline = new PipelineHost();
        await pipeline.StartCoreAsync();

        return pipeline;
    }

    private async Task StartCoreAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(kestrel =>
            kestrel.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2)
        );
        builder.AddOfficeRkyvApi($"Data Source={path}");

        host = builder.Build();
        host.MapOfficeRkyvApi();
        await host.StartAsync();

        var addresses = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        Address = new Uri(addresses.Addresses.Single());
        Channel = GrpcChannel.ForAddress(Address);
    }

    public async ValueTask DisposeAsync()
    {
        Channel.Dispose();
        await host.StopAsync();
        await host.DisposeAsync();
        File.Delete(path);
    }
}
