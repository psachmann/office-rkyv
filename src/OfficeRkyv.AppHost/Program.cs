var builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("officerkyv-db").WithSqliteWeb();

// The key the web host signs caller tokens with and the API validates them with. Generated for each run and not
// persisted: it lives only in this process and the environment of the two it starts. A deployment outside Aspire
// sets CallerTokens__SigningKey for both processes itself.
var callerTokenKey = builder.AddParameter(
    "caller-token-key",
    new GenerateParameterDefault { MinLength = 32, Special = false },
    secret: true,
    persist: false
);

var api = builder
    .AddProject<Projects.OfficeRkyv_Api>("api-svc")
    .WithReference(sqlite)
    .WithEnvironment("CallerTokens__SigningKey", callerTokenKey);

builder
    .AddProject<Projects.OfficeRkyv_Web>("web-svc")
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("CallerTokens__SigningKey", callerTokenKey)
    .WithExternalHttpEndpoints();

builder.Build().Run();
