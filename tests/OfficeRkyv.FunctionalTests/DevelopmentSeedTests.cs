using Microsoft.Playwright;
using TUnit.Core.Interfaces;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The app the tests start has no demo data: the fixture switches the development seed off, so every test
/// builds its own data from an empty database. The other tests do not depend on this, but they would start to
/// fail in confusing ways if the seed leaked into them.
/// </summary>
[ParallelLimiter<BrowserParallelLimit>]
[ClassDataSource<AppHostFixture>(Shared = SharedType.PerTestSession)]
public class DevelopmentSeedTests(AppHostFixture fixture) : PageTest
{
    [Test]
    public async Task TheLandingPage_ListsNoDemoUser()
    {
        var address = fixture.CreateHttpClient("web-svc").BaseAddress?.AbsoluteUri;

        Assert.NotNull(address);

        await Page.GotoAsync(address);

        // "New user" is shown in the empty state and above the list alike, so it marks the end of loading.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "New user" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("demo@example.com")).ToHaveCountAsync(0);
        await Expect(Page.GetByText("Demo User")).ToHaveCountAsync(0);
    }
}
