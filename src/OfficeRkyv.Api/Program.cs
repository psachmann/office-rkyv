using OfficeRkyv.Api;
using OfficeRkyv.Api.Development;
using OfficeRkyv.Api.Shared.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();
builder.AddOfficeRkyvApi(
    builder.Configuration.GetConnectionString("officerkyv-db")
        ?? throw new InvalidProgramException("No DB connection string provided"),
    // SQLite allows one writer per file, so time spent queued for that lock is the first thing to
    // watch as load grows. TrackConnections is deliberately left off: Fisher rejects it, because
    // Microsoft.Data.Sqlite pools connections process-wide and not per store.
    options => options.OpenTelemetry.TrackWriteLockContention()
);

// Development only, and only on the API server: the desktop app shares the composition above but keeps the
// user's real data, so it never seeds.
builder.AddDevelopmentSeedData();
builder.Services.AddApiTelemetry();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOfficeRkyvApi();

app.Run();
