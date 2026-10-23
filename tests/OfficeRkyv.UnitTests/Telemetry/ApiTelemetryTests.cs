using System.Diagnostics;
using System.Net;
using Fisher;
using Grpc.Core;
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
using OfficeRkyv.Api.Features.Archives;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Api.Features.TextExtraction;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.Api.Shared.Auth;
using OfficeRkyv.Api.Shared.Blobs;
using OfficeRkyv.Api.Shared.Errors;
using OfficeRkyv.Api.Shared.Extraction;
using OfficeRkyv.Api.Shared.Telemetry;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;
using OfficeRkyv.UnitTests.Extraction;
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
    private readonly FakeOcr ocr = new();
    private WebApplication host = null!;
    private GrpcChannel channel = null!;

    // ActivitySources are process-wide, so this host's exporter also hears every other host the test run has
    // going in parallel. Kestrel's port is what tells this host's server spans apart from theirs.
    private int port;
    private Uri address = null!;

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
                options.AddAggregateSnapshots();
                options.AddSearchIndexes();
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
            options.LocalQueueFor<ExtractText>().UseDurableInbox().Sequential();
        });
        builder.Services.AddGrpc();

        // What an upload needs besides the store: where the content goes, and the component that stores it.
        builder.Services.AddOptions<FileStorageOptions>();
        var blobs = new FileSystemBlobStore(path + ".blobs");
        builder.Services.AddSingleton(blobs);
        builder.Services.AddSingleton<IBlobStore>(blobs);
        builder.Services.AddSingleton(new TextStore(path + ".blobs"));
        builder.Services.AddSingleton<IPageRenderer>(ocr);
        builder.Services.AddSingleton<IImageReader>(ocr);
        builder.Services.AddSingleton<TextExtractor>();
        builder.Services.AddSingleton<DocumentIngest>();

        // The services read the caller from the token, so this host validates tokens as the API does.
        builder.Services.AddCallerAuthentication(PipelineHost.CallerKey);

        host = builder.Build();
        host.UseAuthentication();
        host.MapGrpcService<UsersServiceImpl>();
        host.MapGrpcService<ArchivesServiceImpl>();
        host.MapGrpcService<DocumentsServiceImpl>();
        await host.StartAsync();

        var addresses = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        address = new Uri(addresses.Addresses.Single());
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
        using var asUser = PipelineHost.ChannelFor(address, Guid.Parse(user.Id));
        var documents = new DocumentsService.DocumentsServiceClient(asUser);

        var document = await TestDocuments.UploadAsync(documents, fileName, TestDocuments.Bytes(2_000), title: title);
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
    public async Task AnUpload_IsFollowedByItsExtraction_WhichIsTraced_AndLeavesNoExtractedTextOnAnySpan()
    {
        // One page has a text layer and one is read by OCR, so that both sources of text are covered.
        const string layerText = "Confidential diagnosis for Ada Lovelace";
        const string ocrText = "Handwritten note about the treatment";
        ocr.TextOfPage[2] = ocrText;
        var user = (
            await Client.CreateUserAsync(new CreateUserRequest { Email = "ada@example.com", DisplayName = "Ada" })
        ).User;
        using var asUser = PipelineHost.ChannelFor(address, Guid.Parse(user.Id));
        var documents = new DocumentsService.DocumentsServiceClient(asUser);

        var document = await TestDocuments.UploadAsync(documents, "results.pdf", TestPdfs.WithPages(layerText, ""));
        var outcome = await WaitForExtraction(document.Sha256);
        var spans = await WaitForServerSpans(2);
        var extractionSpans = await WaitForExtractionSpans();

        // The extraction is traced like any message: a span for handling `ExtractText`, with the event store below it.
        var leaked = spans
            .Concat(extractionSpans)
            .Distinct()
            .Where(span => Everything(span).Any(text => text.Contains(layerText) || text.Contains(ocrText)))
            .ToList();

        await Assert.That(outcome.OcrPageCount).IsEqualTo(1);
        await Assert.That(extractionSpans.Count).IsGreaterThan(0);
        await Assert.That(leaked.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExtractedText_ThatIsSearchable_AppearsOnNoSpan_OfTheExtractionOrTheSearch()
    {
        const string word = "Zahnarztrechnung";
        var user = (
            await Client.CreateUserAsync(new CreateUserRequest { Email = "ada@example.com", DisplayName = "Ada" })
        ).User;
        using var asUser = PipelineHost.ChannelFor(address, Guid.Parse(user.Id));
        var documents = new DocumentsService.DocumentsServiceClient(asUser);

        var document = await TestDocuments.UploadAsync(
            documents,
            "scan0001.pdf",
            TestPdfs.WithPages($"{word} fuer Maerz 2026"),
            title: "Scan"
        );
        await WaitForExtraction(document.Sha256);
        var extractionSpans = await WaitForExtractionSpans();

        // The word is only in the text of the file, so finding the document proves that the text is in the index.
        var response = await documents.SearchDocumentsAsync(new SearchDocumentsRequest { Query = "zahnarzt" });
        var spans = await WaitForServerSpans(3);

        var server = ServerSpanFor(spans, "SearchDocuments");
        var below = spans.Where(span => IsDescendantOf(spans, span, server)).ToList();
        var leaked = spans
            .Concat(extractionSpans)
            .Distinct()
            .Where(span => Everything(span).Any(text => text.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        await Assert.That(response.Documents.Select(found => found.Title)).IsEquivalentTo(["Scan"]);
        await Assert.That(below.Any(span => span.Source.Name == WolverineSource)).IsTrue();
        await Assert.That(extractionSpans.Count).IsGreaterThan(0);
        await Assert.That(leaked.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AQueryString_HasItsValuesRedactedOnTheServerSpan_AndKeepsItsKeys()
    {
        const string searched = "Stromrechnung";

        // The page address of the web host carries what a user searched for (?q=). The endpoint does not exist: a
        // refused request is traced like any other.
        using var http = new HttpClient
        {
            DefaultRequestVersion = System.Net.HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
        await http.GetAsync(new Uri(address, $"/anything?q={searched}&page=2&flag"));
        var spans = await WaitForServerSpans(1);

        var server = spans.Single(IsOwnServerSpan);
        var leaked = spans.Where(span => Everything(span).Any(text => text.Contains(searched))).ToList();

        await Assert.That(server.GetTagItem("url.query")).IsEqualTo("?q=Redacted&page=Redacted&flag");
        await Assert.That(leaked.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Search_LeavesTheQueryOnNoSpan()
    {
        const string query = "Stromrechnung";
        var user = (
            await Client.CreateUserAsync(new CreateUserRequest { Email = "ada@example.com", DisplayName = "Ada" })
        ).User;
        using var asUser = PipelineHost.ChannelFor(address, Guid.Parse(user.Id));
        var documents = new DocumentsService.DocumentsServiceClient(asUser);
        await TestDocuments.UploadAsync(documents, "scan0001.pdf", TestDocuments.Bytes(100), title: "Strom");

        var response = await documents.SearchDocumentsAsync(new SearchDocumentsRequest { Query = query });
        var spans = await WaitForServerSpans(3);

        // The search is traced like any call: a server span with the handler below it. A read opens no store span.
        var server = ServerSpanFor(spans, "SearchDocuments");
        var below = spans.Where(span => IsDescendantOf(spans, span, server)).ToList();
        var leaked = spans.Where(span => Everything(span).Any(text => text.Contains(query))).ToList();

        await Assert.That(below.Any(span => span.Source.Name == WolverineSource)).IsTrue();
        await Assert.That(leaked.Count).IsEqualTo(0);
        await Assert.That(response.Documents).IsEmpty();
    }

    [Test]
    public async Task ACallerToken_AppearsOnNoSpan_NotEvenInPart()
    {
        var user = (
            await Client.CreateUserAsync(new CreateUserRequest { Email = "ada@example.com", DisplayName = "Ada" })
        ).User;
        var token = PipelineHost.TokenFor(Guid.Parse(user.Id));
        var headers = new Metadata { { "authorization", $"Bearer {token}" } };

        await new ArchivesService.ArchivesServiceClient(channel).ListArchivesAsync(
            new ListArchivesRequest(),
            new CallOptions(headers)
        );
        var spans = await WaitForServerSpans(2);

        // The call was accepted and traced, so a pass is not for want of anything to look at.
        var server = ServerSpanFor(spans, "ListArchives");
        var below = spans.Where(span => IsDescendantOf(spans, span, server)).ToList();
        var parts = token.Split('.');
        var leaked = spans
            .Where(span => Everything(span).Any(text => text.Contains(token) || parts.Any(text.Contains)))
            .ToList();

        await Assert.That(below.Any(span => span.Source.Name == WolverineSource)).IsTrue();
        await Assert.That(leaked.Count).IsEqualTo(0);
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

    private async Task<TextExtraction> WaitForExtraction(string sha256)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var found = await scope
                .ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync<TextExtraction?>(new FindTextExtractionByHashQuery(sha256));

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The extraction never completed.");
    }

    // The span of a handled message ends after the handler has committed, so it can land a moment after the outcome.
    private async Task<List<Activity>> WaitForExtractionSpans()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var found = activities
                .ToArray()
                .Where(span =>
                    span.Source.Name == WolverineSource
                    && Everything(span).Any(text => text.Contains(nameof(ExtractText)))
                )
                .ToList();

            if (found.Count > 0)
            {
                return found;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("No span for handling ExtractText arrived.");
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
