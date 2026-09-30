using OfficeRkyv.Api.Features.Archives;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Services;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.Api.Shared.Errors;
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
    /// Registers the event store (Fisher on SQLite), Wolverine with its error mapping, and gRPC.
    /// </summary>
    /// <param name="builder">The host's builder.</param>
    /// <param name="connectionString">The SQLite connection string of the event store.</param>
    /// <param name="configureStore">Host-specific store settings, such as telemetry.</param>
    public static WebApplicationBuilder AddOfficeRkyvApi(
        this WebApplicationBuilder builder,
        string connectionString,
        Action<StoreOptions>? configureStore = null
    )
    {
        builder
            .Services.AddFisher(options =>
            {
                options.Connection(connectionString);
                options.AddAggregateSnapshots();
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
        });
        builder.Services.AddGrpc();

        return builder;
    }

    /// <summary>Maps the API's gRPC services.</summary>
    public static IEndpointRouteBuilder MapOfficeRkyvApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<GreeterService>();
        endpoints.MapGrpcService<UsersServiceImpl>();
        endpoints.MapGrpcService<ArchivesServiceImpl>();
        endpoints.MapGrpcService<DocumentsServiceImpl>();

        return endpoints;
    }
}
