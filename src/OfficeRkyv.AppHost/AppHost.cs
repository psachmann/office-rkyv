var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.OfficeRkyv_Api>("api");

builder.AddProject<Projects.OfficeRkyv_Web>("web").WithReference(api).WaitFor(api).WithExternalHttpEndpoints();

builder.Build().Run();
