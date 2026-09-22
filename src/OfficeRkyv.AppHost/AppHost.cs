var builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("sqlite");

var api = builder.AddProject<Projects.OfficeRkyv_Api>("api").WithReference(sqlite);

builder.AddProject<Projects.OfficeRkyv_Web>("web").WithReference(api).WaitFor(api).WithExternalHttpEndpoints();

builder.Build().Run();
