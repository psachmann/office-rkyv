using Fisher;
using JasperFx.Events.Projections;
using OfficeRkyv.Api;
using OfficeRkyv.Api.Data;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Features.Users.Services;
using OfficeRkyv.Api.Services;
using Wolverine;
using Wolverine.Fisher;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

// NOTE: EF Core and Fisher must not open independent connections to the same SQLite file - two
// writers on one file self-deadlock from inside the first one's transaction, and it presents as a
// hang rather than an error. They are deliberately separate files until the DbContext is enlisted in
// Fisher's transaction via Fisher.EntityFrameworkCore.
builder.AddSqliteDbContext<OfficeRkyvContext>("sqlite");

builder
    .Services.AddFisher(options =>
    {
        options.Connection("Data Source=officerkyv-events.db");

        // Inline: the snapshot document is written in the same transaction as the event append, so a
        // query immediately after a command never sees stale state. This is what lets the UI's
        // invalidate-then-refetch cycle be correct rather than merely usually correct.
        options.Projections.Snapshot<User>(SnapshotLifecycle.Inline);
    })
    .ApplyAllDatabaseChangesOnStartup()
    .IntegrateWithWolverine();

builder.Host.UseWolverine(options =>
{
    // Single process, single SQLite file: there is no second node to fail over to.
    options.Durability.Mode = DurabilityMode.Solo;
    options.Policies.AutoApplyTransactions();
});

builder.Services.AddGrpc();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<UserCommandsService>();
app.MapGrpcService<UserQueriesService>();
app.MapGet(
    "/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909"
);

app.Run();
