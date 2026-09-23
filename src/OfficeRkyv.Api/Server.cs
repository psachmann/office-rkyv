using Fisher;
using JasperFx.Events.Projections;
using OfficeRkyv.Api;
using OfficeRkyv.Api.Data;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Features.Users.Services;
using OfficeRkyv.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();
builder.AddSqliteDbContext<OfficeRkyvContext>("sqlite");
builder
    .Services.AddFisher(options =>
    {
        options.Connection(builder.Configuration.GetConnectionString("sqlite") ?? "Data Source=officerkyv.db");

        // Inline: the snapshot document is written in the same transaction as the event append,
        // so a query immediately after a command never sees stale state.
        options.Projections.Snapshot<UserEntity>(SnapshotLifecycle.Inline);
    })
    .ApplyAllDatabaseChangesOnStartup();
builder.Services.AddGrpc();
builder.Services.AddMediator(options =>
{
    options.Namespace = "OfficeRkyv.Api.Mediator";
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.GenerateTypesAsInternal = true;
    options.NotificationPublisherType = typeof(Mediator.ForeachAwaitPublisher);
    options.Assemblies = [OfficeRkyvApiLib.Assembly];
    options.PipelineBehaviors = [];
    options.StreamPipelineBehaviors = [];
});

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
