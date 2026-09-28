using OfficeRkyv.Api;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Services;
using OfficeRkyv.Api.Shared.Errors;
using OfficeRkyv.Api.Shared.Telemetry;
using Wolverine;
using Wolverine.Fisher;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();
builder
    .Services.AddFisher(services =>
    {
        var config = services.GetRequiredService<IConfiguration>();
        var options = new StoreOptions();

        options.Connection(
            config.GetConnectionString("officerkyv-db")
                ?? throw new InvalidProgramException("No DB connection string provided")
        );
        options.Projections.Snapshot<User>(SnapshotLifecycle.Inline);

        // SQLite allows one writer per file, so time spent queued for that lock is the first thing to
        // watch as load grows. TrackConnections is deliberately left off: Fisher rejects it, because
        // Microsoft.Data.Sqlite pools connections process-wide and not per store.
        options.OpenTelemetry.TrackWriteLockContention();

        return options;
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
builder.Services.AddApiTelemetry();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGrpcService<GreeterService>();
app.MapGrpcService<UsersServiceImpl>();

app.Run();
