using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The AppHost the functional tests share, with the development seed switched off for the API. The API runs in
/// the Development environment here, where it would otherwise seed a demo user into the database the tests
/// expect to start empty.
/// </summary>
public sealed class AppHostFixture : AspireFixture<Projects.OfficeRkyv_AppHost>
{
    /// <summary>The name of the API's resource in the AppHost.</summary>
    public const string ApiResource = "api-svc";

    protected override void ConfigureBuilder(IDistributedApplicationTestingBuilder builder)
    {
        builder.CreateResourceBuilder<ProjectResource>(ApiResource).WithEnvironment("Seed__DemoData", "false");

        base.ConfigureBuilder(builder);
    }
}
