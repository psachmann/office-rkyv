using System.Diagnostics;
using System.Net;
using Fisher;
using Grpc.Net.Client;
using JasperFx.Events.Projections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeRkyv.Api;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Shared.Blobs;
using OfficeRkyv.Api.Shared.Errors;
using OfficeRkyv.Api.Shared.Telemetry;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Wolverine;
using Wolverine.Fisher;

namespace OfficeRkyv.UnitTests.Telemetry;

/// <summary>
/// Serves the real gRPC service over the real Wolverine pipeline and Fisher store, wires telemetry with the
/// same <see cref="TelemetryExtensions.AddApiTelemetry"/> the app calls, and reads what comes out of an
/// in-memory exporter. Covers the API half of the observability spec; a typo in one of the plain-string
/// source names shows up here as a missing span or metric.
/// </summary>
public sealed class ApiTelemetryTests : IAsyncDisposable
{
    private const string FisherSource = "Fisher";
    private const string WolverineSource = "Wolverine";

    private readonly string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.db");
    private readonly List<Activity> activities = [];
    private readonly List<Metric> metrics = [];
    private WebApplication host = null!;
    private GrpcChannel channel = null!;

    // ActivitySources are process-wide, so this host's exporter also hears every other host the test run has
    // going in parallel. Kestrel's port is what tells this host's server spans apart from theirs.
    private int port;

    private UsersService.UsersServiceClient Client => new(channel);

    [Before(Test)]
    public async Task SetUp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(kestrel =>
            kestrel.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2)
        );

        // The base every service gets from ServiceDefaults, which is where the ASP.NET Core server span comes from.
        builder.ConfigureOpenTelemetry();
        builder.Services.AddApiTelemetry();
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(activities));
        builder.Services.ConfigureOpenTelemetryMeterProvider(meters => meters.AddInMemoryExporter(metrics));

        builder
            .Services.AddFisher(options =>
            {
                options.Connection($"Data Source={path}");
                options.Projections.Snapshot<User>(SnapshotLifecycle.Inline);
                options.Projections.Snapshot<Document>(SnapshotLifecycle.Inline);
                options.OpenTelemetry.TrackWriteLockContention();
            })
            .ApplyAllDatabaseChangesOnStartup()
            .IntegrateWithWolverine();
        builder.Host.UseWolverine(options =>
        {
            options.ApplicationAssembly = OfficeRkyvApiLib.Assembly;
            options.Durability.Mode = DurabilityMode.Solo;
            options.Policies.AutoApplyTransactions();
            options.UseApiErrorMapping();
        });
        builder.Services.AddGrpc();

        // What an upload needs besides the store: where the content goes, and the component that stores it.
        builder.Services.AddOptions<FileStorageOptions>();
        builder.Services.AddSingleton<IBlobStore>(new FileSystemBlobStore(path + ".blobs"));
        builder.Services.AddSingleton<DocumentIngest>();

        host = builder.Build();
        host.MapGrpcService<UsersServiceImpl>();
        host.MapGrpcService<DocumentsServiceImpl>();
        await host.StartAsync();

        var addresses = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        var address = new Uri(addresses.Addresses.Single());
        port = address.Port;
        channel = GrpcChannel.ForAddress(address);
    }

    [Test]
    public async Task GrpcCall_ProducesHandlerSpanUnderTheRequestSpan()
    {
        await Client.ListUsersAsync(new ListUsersRequest());
        var spans = await WaitForServerSpans(1);

        var server = ServerSpanFor(spans, "ListUsers");
        var handlers = spans.Where(span => span.Source.Name == WolverineSource && IsDescendantOf(spans, span, server));

        await Assert.That(handlers.Any()).IsTrue();
    }

    [Test]
    public async Task Command_ProducesStoreSpanUnderItsHandlerSpan()
    {
        await Client.CreateUserAsync(new CreateUserRequest { Email = "ada@example.com", DisplayName = "Ada Lovelace" });
        var spans = await WaitForServerSpans(1);

        var server = ServerSpanFor(spans, "CreateUser");
        var handlers = spans.Where(span => span.Source.Name == WolverineSource && IsDescendantOf(spans, span, server));
        var storeSpans = spans.Where(span =>
            span.Source.Name == FisherSource && handlers.Any(handler => IsDescendantOf(spans, span, handler))
        );

        await Assert.That(handlers.Any()).IsTrue();
        await Assert.That(storeSpans.Any()).IsTrue();
    }

    [Test]
    public async Task Command_LeavesNoUserDataOnStoreSpans()
    {
        const string email = "ada@example.com";
        await Client.CreateUserAsync(new CreateUserRequest { Email = email, DisplayName = "Ada Lovelace" });
        var spans = await WaitForServerSpans(1);

        var storeSpans = spans.Where(span => span.Source.Name == FisherSource).ToList();
        var leaked = storeSpans.Where(span => Everything(span).Any(text => text.Contains(email))).ToList();

        // Guards against a vacuous pass: with no store spans there would be nothing to leak from.
        await Assert.That(storeSpans.Count).IsGreaterThan(0);
        await Assert.That(leaked.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Upload_LeavesNeitherTheFileNameNorTheTitleOnAnySpan()
    {
        const string fileName = "diagnosis-results.pdf";
        const string title = "Confidential diagnosis";
        var user = (
            await Client.CreateUserAsync(new CreateUserRequest { Email = "ada@example.com", DisplayName = "Ada" })
        ).User;
        var documents = new DocumentsService.DocumentsServiceClient(channel);

        var document = await TestDocuments.UploadAsync(
            documents,
            user.Id,
            fileName,
            TestDocuments.Bytes(2_000),
            title: title
        );
        var spans = await WaitForServerSpans(2);

        // The upload is traced like any call: a server span, with the command and the append below it.
        var server = ServerSpanFor(spans, "UploadDocument");
        var below = spans.Where(span => IsDescendantOf(spans, span, server)).ToList();
        var leaked = spans
            .Where(span => Everything(span).Any(text => text.Contains(fileName) || text.Contains(title)))
            .ToList();

        await Assert.That(below.Any(span => span.Source.Name == FisherSource)).IsTrue();
        await Assert.That(leaked.Count).IsEqualTo(0);
        await Assert.That(document.Title).IsEqualTo(title);
    }

    [Test]
    public async Task Command_ExportsWriteLockAndMessageHandlingMetrics()
    {
        await Client.CreateUserAsync(new CreateUserRequest { Email = "ada@example.com", DisplayName = "Ada Lovelace" });

        // The reader exports on a timer; flush instead of waiting for it.
        host.Services.GetRequiredService<MeterProvider>().ForceFlush(10_000);
        var exported = metrics.ToArray();

        await Assert.That(exported.Any(metric => metric.Name == "fisher.write_lock.wait")).IsTrue();
        await Assert.That(exported.Any(metric => metric.MeterName.StartsWith("Wolverine"))).IsTrue();
    }

    // A server span stops after the response is written, so it can land a moment after the client returns.
    private async Task<Activity[]> WaitForServerSpans(int expected)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var snapshot = activities.ToArray();
            if (snapshot.Count(IsOwnServerSpan) >= expected)
            {
                return snapshot;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Expected {expected} server span(s) but they never arrived.");
    }

    private bool IsOwnServerSpan(Activity span) =>
        span.Kind == ActivityKind.Server && span.GetTagItem("server.port") is int spanPort && spanPort == port;

    private Activity ServerSpanFor(Activity[] spans, string rpc) =>
        spans.Single(span =>
            IsOwnServerSpan(span) && (span.GetTagItem("url.path") as string)?.EndsWith($"/{rpc}") == true
        );

    private static bool IsDescendantOf(Activity[] spans, Activity span, Activity ancestor)
    {
        if (span.TraceId != ancestor.TraceId)
        {
            return false;
        }

        for (var parent = span.ParentSpanId; parent != default; )
        {
            if (parent == ancestor.SpanId)
            {
                return true;
            }

            parent = spans.FirstOrDefault(candidate => candidate.SpanId == parent)?.ParentSpanId ?? default;
        }

        return false;
    }

    // Every string a backend could show a person for this span: name, tags, and the tags on its events.
    private static IEnumerable<string> Everything(Activity span) =>
        new[] { span.DisplayName }
            .Concat(span.TagObjects.Select(tag => tag.Value?.ToString() ?? ""))
            .Concat(span.Events.SelectMany(@event => @event.Tags).Select(tag => tag.Value?.ToString() ?? ""));

    public async ValueTask DisposeAsync()
    {
        channel.Dispose();
        await host.StopAsync();
        await host.DisposeAsync();
        File.Delete(path);

        if (Directory.Exists(path + ".blobs"))
        {
            Directory.Delete(path + ".blobs", recursive: true);
        }
    }
}
