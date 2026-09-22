using TUnit.Core.Interfaces;

namespace OfficeRkyv.FunctionalTests;

public class BrowserParallelLimit : IParallelLimit
{
    public int Limit => 1;
}

[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AspireFixture<Projects.OfficeRkyv_AppHost>>(Shared = SharedType.PerTestSession)]
public class UiLocalizationTests(AspireFixture<Projects.OfficeRkyv_AppHost> fixture) : PageTest
{
    [Before(Test)]
    public async Task Before()
    {
        var address = fixture.CreateHttpClient("web").BaseAddress?.AbsoluteUri;

        Assert.NotNull(address);

        await Page.GotoAsync(address);
    }

    [Test]
    public async Task HelloWorldTest()
    {
        var heading = await Page.Locator("h1").TextContentAsync();

        await Assert.That(heading).IsEqualTo("Hello, world!");
    }
}
