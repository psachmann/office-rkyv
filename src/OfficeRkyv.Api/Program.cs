using Fisher;
using JasperFx.Events.Projections;
using OfficeRkyv.Api;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Features.Users.Services;
using OfficeRkyv.Api.Services;
using Wolverine;
using Wolverine.Fisher;
using Wolverine.Grpc;

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
});
builder.Services.AddGrpc();
builder.Services.AddWolverineGrpc();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapWolverineGrpcServices();

// Configure the HTTP request pipeline.
app.MapGrpcService<UserCommandsService>();
app.MapGrpcService<UserQueriesService>();

app.Run();
