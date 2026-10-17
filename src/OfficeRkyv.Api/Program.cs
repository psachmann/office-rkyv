using System.Text;
using OfficeRkyv.Api;
using OfficeRkyv.Api.Development;
using OfficeRkyv.Api.Shared.Blobs;
using OfficeRkyv.Api.Shared.Telemetry;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("officerkyv-db")
    ?? throw new InvalidProgramException("No DB connection string provided");

// The web host signs the caller tokens with the same key; the AppHost hands both processes one.
var callerTokenKey = Encoding.UTF8.GetBytes(
    builder.Configuration[CallerTokens.SigningKeySetting]
        ?? throw new InvalidProgramException($"No caller token signing key provided ({CallerTokens.SigningKeySetting})")
);

// Add services to the container.
builder.AddServiceDefaults();
builder.AddOfficeRkyvApi(
    connectionString,
    // Beside the database file unless configured, so that content has the database's lifetime: the AppHost
    // gives the API a new temporary database on every run.
    BlobDirectory.For(connectionString, builder.Configuration[FileStorageOptions.BlobDirectorySetting]),
    callerTokenKey,
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
