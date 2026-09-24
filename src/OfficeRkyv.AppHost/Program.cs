var builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("officerkyv-db").WithSqliteWeb();

var api = builder.AddProject<Projects.OfficeRkyv_Api>("api-svc").WithReference(sqlite);

builder.AddProject<Projects.OfficeRkyv_Web>("web-svc").WithReference(api).WaitFor(api).WithExternalHttpEndpoints();

builder.Build().Run();
