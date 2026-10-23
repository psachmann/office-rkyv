using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Fisher;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeRkyv.Api;
using OfficeRkyv.Api.Features.TextExtraction;
using OfficeRkyv.Api.Features.TextExtraction.Handlers;
using OfficeRkyv.Api.Shared.Blobs;
using OfficeRkyv.Api.Shared.Extraction;
using OfficeRkyv.Contracts;
using OfficeRkyv.UI.Auth;
using OfficeRkyv.UnitTests.Extraction;
using Wolverine;
using Wolverine.Runtime;
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
    private bool stopped;

    private PipelineHost(string? databasePath) =>
        path = databasePath ?? Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.db");

    private AsyncServiceScope scope;

    /// <summary>
    /// Resolved through a scope that lives as long as the host: the bus is a scoped service, which the root
    /// provider refuses in the Development environment, where scopes are validated.
    /// </summary>
    public IMessageBus Bus => scope.ServiceProvider.GetRequiredService<IMessageBus>();

    public IDocumentStore Store => host.Services.GetRequiredService<IDocumentStore>();

    /// <summary>The database file, for starting a host on the same data once this one is stopped.</summary>
    public string DatabasePath => path;

    /// <summary>The text store of this host, over the same directory as its blob store.</summary>
    public TextStore Texts => host.Services.GetRequiredService<TextStore>();

    /// <summary>The extraction outcome recorded for the content, or null while there is none.</summary>
    public async Task<TextExtraction?> FindExtractionAsync(string sha256)
    {
        await using var queryScope = host.Services.CreateAsyncScope();

        return await queryScope
            .ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<TextExtraction?>(new FindTextExtractionByHashQuery(sha256));
    }

    /// <summary>The searchable text kept for the content, or null while there is none.</summary>
    public async Task<ContentText?> FindContentTextAsync(string sha256)
    {
        await using var query = Store.QuerySession();

        return await query.LoadAsync<ContentText>(sha256);
    }

    /// <summary>
    /// Waits until the host has made the texts that were extracted before it started searchable (the start-up work
    /// of <c>ContentTextIndexer</c>), which runs in the background after the host has started.
    /// </summary>
    public Task WaitForIndexingAsync() =>
        host.Services.GetServices<IHostedService>().OfType<ContentTextIndexer>().Single().ExecuteTask
        ?? Task.CompletedTask;

    /// <summary>How many contents have searchable text.</summary>
    public async Task<int> CountContentTextsAsync()
    {
        await using var query = Store.QuerySession();

        return (
            await query.AdvancedSql.QueryAsync<string>("select id from fi_doc_contenttext", CancellationToken.None)
        ).Count;
    }

    /// <summary>
    /// Waits until the extraction of the content has been processed, which is when its outcome exists. The
    /// extraction runs in the background after an upload has been answered, so a test that asserts on its result
    /// waits here first.
    /// </summary>
    public async Task<TextExtraction> WaitForExtractionAsync(string sha256, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < deadline)
        {
            if (await FindExtractionAsync(sha256) is { } outcome)
            {
                return outcome;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"No extraction outcome for {sha256} appeared.");
    }

    /// <summary>
    /// Waits until every extraction that has been queued is processed, whether or not it recorded an outcome. For
    /// a test that has to know that a second message for the same content has been handled and did nothing.
    /// </summary>
    public async Task WaitForExtractionsToSettleAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        var admin = host.Services.GetRequiredService<IWolverineRuntime>().Storage.Admin;

        while (DateTime.UtcNow < deadline)
        {
            if ((await admin.FetchCountsAsync()).Incoming == 0)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Queued extractions were still not processed.");
    }

    /// <summary>
    /// Stops the host and keeps its data, so that <see cref="StartAsync"/> can start another on the same
    /// <see cref="DatabasePath"/>. Disposing the host still deletes the data.
    /// </summary>
    public async Task StopKeepingDataAsync()
    {
        stopped = true;
        await host.StopAsync();
    }

    public IHost Host => host;

    /// <summary>
    /// Invokes the message and waits until everything it cascaded has been handled too - the outbox
    /// deliveries included - so a test can assert on the settled state.
    /// </summary>
    public Task<ITrackedSession> InvokeAndSettleAsync(object message) => host.InvokeMessageAndWaitAsync(message);

    /// <summary>A channel that carries no caller token: what a signed-out client sees.</summary>
    public GrpcChannel Channel { get; private set; } = null!;

    public Uri Address { get; private set; } = null!;

    /// <summary>
    /// The key the host validates caller tokens with: a fixed test key, so a token made for another key is one the
    /// API does not trust.
    /// </summary>
    public static byte[] CallerKey { get; } = Encoding.UTF8.GetBytes("pipeline-host-caller-token-key-0123456789");

    /// <summary>A key the host does not trust, for the tokens a test wants refused.</summary>
    public static byte[] OtherKey { get; } = Encoding.UTF8.GetBytes("some-other-caller-token-key-0123456789ab");

    /// <summary>
    /// A caller token for the user, made by the same issuer the hosts use. Without arguments it is valid; a test
    /// that needs a refused one passes a key the API does not trust, or an expiry in the past (beyond the
    /// validation's five minutes of clock skew).
    /// </summary>
    public static string TokenFor(Guid userId, byte[]? key = null, DateTimeOffset? expires = null)
    {
        var issuedAt = expires is { } expiry ? new FixedTime(expiry - CallerTokens.Lifetime) : TimeProvider.System;

        return new CallerTokenIssuer(key ?? CallerKey, issuedAt).Issue(userId);
    }

    /// <summary>The headers a call needs to be made as the user, for a call on <see cref="Channel"/>.</summary>
    public static CallOptions CallOptionsFor(Guid userId, byte[]? key = null, DateTimeOffset? expires = null) =>
        new(new Metadata { { "authorization", $"Bearer {TokenFor(userId, key, expires)}" } });

    /// <summary>
    /// A channel on which every call is made as the user, as the desktop's clients do it: the token comes from call
    /// credentials, which a channel without TLS only uses when told to. The host disposes the channel.
    /// </summary>
    public GrpcChannel ChannelFor(Guid userId, byte[]? key = null, DateTimeOffset? expires = null)
    {
        var channel = ChannelFor(Address, userId, key, expires);
        userChannels.Add(channel);

        return channel;
    }

    /// <summary>
    /// A channel to any test host on which every call is made as the user. The caller disposes it.
    /// </summary>
    public static GrpcChannel ChannelFor(Uri address, Guid userId, byte[]? key = null, DateTimeOffset? expires = null)
    {
        var credentials = CallCredentials.FromInterceptor(
            (_, metadata) =>
            {
                metadata.Add("authorization", $"Bearer {TokenFor(userId, key, expires)}");

                return Task.CompletedTask;
            }
        );

        return GrpcChannel.ForAddress(
            address,
            new GrpcChannelOptions
            {
                Credentials = ChannelCredentials.Create(ChannelCredentials.Insecure, credentials),
                UnsafeUseInsecureChannelCallCredentials = true,
            }
        );
    }

    /// <inheritdoc cref="ChannelFor(Guid, byte[], DateTimeOffset?)"/>
    public GrpcChannel ChannelFor(string userId, byte[]? key = null, DateTimeOffset? expires = null) =>
        ChannelFor(Guid.Parse(userId), key, expires);

    private readonly ConcurrentBag<GrpcChannel> userChannels = [];

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <param name="environmentName">The host environment; the default is what the test run itself reports.</param>
    /// <param name="configure">Runs after the API composition, for a host-specific addition such as the seed.</param>
    /// <param name="maxFileSizeBytes">The largest accepted file; a test that needs a limit it can reach sets it.</param>
    /// <param name="databasePath">
    /// An existing database file to start on, with the blob directory beside it, instead of a new one: a test that
    /// needs data written by an earlier version, or by an earlier host. The host deletes both when it is disposed.
    /// </param>
    /// <param name="ocr">
    /// Stands in for the OCR programs, so that no test needs them installed; without it the host would run the real
    /// ones if they are on <c>PATH</c>. Real-program tests leave it out.
    /// </param>
    public static async Task<PipelineHost> StartAsync(
        string? environmentName = null,
        Action<WebApplicationBuilder>? configure = null,
        long? maxFileSizeBytes = null,
        string? databasePath = null,
        FakeOcr? ocr = null
    )
    {
        var pipeline = new PipelineHost(databasePath);
        await pipeline.StartCoreAsync(environmentName, configure, maxFileSizeBytes, ocr);

        return pipeline;
    }

    private async Task StartCoreAsync(
        string? environmentName,
        Action<WebApplicationBuilder>? configure,
        long? maxFileSizeBytes,
        FakeOcr? ocr
    )
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
        builder.WebHost.UseKestrel(kestrel =>
            kestrel.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2)
        );
        builder.AddOfficeRkyvApi($"Data Source={path}", BlobDirectory, CallerKey);

        if (maxFileSizeBytes is { } limit)
        {
            builder.Services.Configure<FileStorageOptions>(options => options.MaxFileSizeBytes = limit);
        }

        if (ocr is not null)
        {
            // Later registrations win: the pages are rendered and read by the fake and not by programs.
            builder.Services.AddSingleton<IPageRenderer>(ocr);
            builder.Services.AddSingleton<IImageReader>(ocr);
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

        foreach (var channel in userChannels)
        {
            channel.Dispose();
        }

        if (!stopped)
        {
            await host.StopAsync();
        }

        await host.DisposeAsync();
        File.Delete(path);

        if (Directory.Exists(BlobDirectory))
        {
            Directory.Delete(BlobDirectory, recursive: true);
        }
    }
}
