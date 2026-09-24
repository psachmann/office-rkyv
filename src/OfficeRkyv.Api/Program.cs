using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Services;
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

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGrpcService<GreeterService>();
app.MapGrpcService<UserGrpcService>();

app.Run();
