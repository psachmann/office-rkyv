using OfficeRkyv.Api.Features.Archives;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Api.Features.TextExtraction;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.Api.Shared.Auth;
using OfficeRkyv.Api.Shared.Blobs;
using OfficeRkyv.Api.Shared.Errors;
using OfficeRkyv.Api.Shared.Extraction;
using Wolverine;
using Wolverine.Fisher;

namespace OfficeRkyv.Api;

/// <summary>
/// The API's composition, in one place, for every process that hosts it: the API server, the desktop app and
/// the tests. A host adds what is its own (service defaults and telemetry, sockets or ports, where the data
/// lives) around these two calls.
/// </summary>
public static class OfficeRkyvApiExtensions
{
    /// <summary>
    /// Registers the event store (Fisher on SQLite), the file content store, Wolverine with its error mapping,
    /// caller authentication, and gRPC.
    /// </summary>
    /// <param name="builder">The host's builder.</param>
    /// <param name="connectionString">The SQLite connection string of the event store.</param>
    /// <param name="blobDirectory">
    /// Where file content lives. It belongs to the database: both are the same user's data, and a database that
    /// is replaced needs its content replaced with it.
    /// </param>
    /// <param name="callerTokenKey">
    /// The key the UI host signs caller tokens with (see <see cref="CallerTokens"/>); at least
    /// <see cref="CallerTokens.MinimumKeyLength"/> bytes. A host reads it from <c>CallerTokens:SigningKey</c>, or
    /// makes one when only its own process knows both sides.
    /// </param>
    /// <param name="configureStore">Host-specific store settings, such as telemetry.</param>
    public static WebApplicationBuilder AddOfficeRkyvApi(
        this WebApplicationBuilder builder,
        string connectionString,
        string blobDirectory,
        byte[] callerTokenKey,
        Action<StoreOptions>? configureStore = null
    )
    {
        builder.Services.AddCallerAuthentication(callerTokenKey);

        builder
            .Services.AddFisher(options =>
            {
                options.Connection(connectionString);
                options.AddAggregateSnapshots();
                options.AddSearchIndexes();
                configureStore?.Invoke(options);
            })
            .ApplyAllDatabaseChangesOnStartup()
            .IntegrateWithWolverine();

        builder.Host.UseWolverine(options =>
        {
            // Single process, single SQLite file: there is no second node to fail over to.
            options.ApplicationAssembly = OfficeRkyvApiLib.Assembly;
            options.Durability.Mode = DurabilityMode.Solo;
            options.Policies.AutoApplyTransactions();
            options.UseApiErrorMapping();

            // Durable, so an extraction that was waiting when the app stopped is done after it starts again.
            // Sequential, because OCR takes seconds a page and all of a core, and because it makes the handler
            // the only writer of extraction outcomes.
            options.LocalQueueFor<ExtractText>().UseDurableInbox().Sequential();
        });
        builder.Services.AddGrpc();

        builder.Services.AddOptions<FileStorageOptions>().BindConfiguration(FileStorageOptions.SectionName);
        builder.Services.AddSingleton(_ => new FileSystemBlobStore(blobDirectory));
        builder.Services.AddSingleton<IBlobStore>(services => services.GetRequiredService<FileSystemBlobStore>());
        builder.Services.AddSingleton(_ => new TextStore(blobDirectory));
        builder.Services.AddSingleton<DocumentIngest>();
        builder.Services.AddHostedService<BlobStoreCleanupService>();

        builder.Services.AddOptions<TextExtractionOptions>().BindConfiguration(TextExtractionOptions.SectionName);
        builder.Services.AddSingleton<IProgramRunner, ProgramRunner>();
        builder.Services.AddSingleton<IPageRenderer, PdftoppmPageRenderer>();
        builder.Services.AddSingleton<IImageReader, TesseractImageReader>();
        builder.Services.AddSingleton<TextExtractor>();
        builder.Services.AddHostedService<OcrProgramsStartCheck>();

        return builder;
    }

    /// <summary>Reads the caller token on each call, and maps the API's gRPC services.</summary>
    public static WebApplication MapOfficeRkyvApi(this WebApplication app)
    {
        app.UseAuthentication();

        app.MapGrpcService<UsersServiceImpl>();
        app.MapGrpcService<ArchivesServiceImpl>();
        app.MapGrpcService<DocumentsServiceImpl>();

        return app;
    }
}
