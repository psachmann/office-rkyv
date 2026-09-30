extern alias web;

using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OfficeRkyv.UI.Settings;
using OfficeRkyv.UI.Shared;
using web::OfficeRkyv.Web.Settings;

namespace OfficeRkyv.UnitTests.Web;

public sealed class WebUserSettingsServiceTests
{
    private readonly FakeJs js = new();
    private readonly FakeNavigation navigation = new("https://localhost/");

    private WebUserSettingsService CreateService(FakeNavigation? navigationOverride = null) =>
        new(js, navigationOverride ?? navigation);

    // Collects what a subscriber sees, as a component's subscription would.
    private static List<UserSettings> Observe(WebUserSettingsService service)
    {
        var seen = new List<UserSettings>();
        service.Settings.Subscribe(seen.Add);

        return seen;
    }

    [Test]
    public async Task ANewSubscriber_FirstReceivesTheSettingsInForce()
    {
        var service = CreateService();
        service.Initialize(AppearancePreference.Dark, new CultureInfo("en"));

        var seen = Observe(service);

        await Assert.That(seen).Count().IsEqualTo(1);
        await Assert.That(seen[0].Appearance).IsEqualTo(AppearancePreference.Dark);
        await Assert.That(seen[0]).IsEqualTo(service.Current);
    }

    [Test]
    public async Task Initialize_AdoptsWhatThePageWasRenderedWith_WithoutWritingAnythingBack()
    {
        var service = CreateService();
        var seen = Observe(service);

        service.Initialize(AppearancePreference.Dark, new CultureInfo("en-US"));

        // The request's culture is matched to a supported one, like the request localization does.
        await Assert.That(service.Current.Appearance).IsEqualTo(AppearancePreference.Dark);
        await Assert.That(service.Current.Culture.Name).IsEqualTo("en");
        await Assert.That(seen).Count().IsEqualTo(2); // the replayed default, then the adopted settings
        await Assert.That(js.Calls).IsEmpty();
        await Assert.That(navigation.Navigations).IsEmpty();
    }

    [Test]
    public async Task Initialize_WithTheSettingsAlreadyInForce_EmitsNothing()
    {
        var service = CreateService();
        service.Initialize(AppearancePreference.Dark, new CultureInfo("en"));
        var seen = Observe(service);

        service.Initialize(AppearancePreference.Dark, new CultureInfo("en-GB"));

        await Assert.That(seen).Count().IsEqualTo(1); // only the replay on subscribe
    }

    [Test]
    public async Task ChangingTheAppearance_AppliesItThroughJs_WithTheCookieValueAndTheStylesheets_AndEmitsOnce()
    {
        var service = CreateService();
        var seen = Observe(service);

        await service.SetAppearanceAsync(AppearancePreference.Dark);

        await Assert.That(service.Current.Appearance).IsEqualTo(AppearancePreference.Dark);
        await Assert.That(seen).Count().IsEqualTo(2); // the replay, then the change
        await Assert.That(js.ImportedModules).IsEquivalentTo(["./_content/OfficeRkyv.UI/js/appearance.js"]);

        var call = js.Module.Calls.Single();
        await Assert.That(call.Identifier).IsEqualTo("applyAppearance");
        await Assert.That(call.Args![0]).IsEqualTo(Appearance.CookieName);
        await Assert.That(call.Args![1]).IsEqualTo("dark");
        await Assert.That(call.Args![2]).IsEquivalentTo(Appearance.Stylesheets(AppearancePreference.Dark));
    }

    [Test]
    public async Task SettingTheAppearanceAlreadyInForce_EmitsNothing_AndTouchesNothing()
    {
        var service = CreateService();
        var seen = Observe(service);

        await service.SetAppearanceAsync(AppearancePreference.System); // the default

        await Assert.That(seen).Count().IsEqualTo(1);
        await Assert.That(js.Calls).IsEmpty();
    }

    [Test]
    public async Task ChangingTheAppearanceTwice_ImportsTheModuleOnce()
    {
        var service = CreateService();

        await service.SetAppearanceAsync(AppearancePreference.Dark);
        await service.SetAppearanceAsync(AppearancePreference.Light);

        await Assert.That(js.ImportedModules).Count().IsEqualTo(1);
        await Assert.That(js.Module.Calls).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ChangingTheCulture_NavigatesToTheCultureEndpoint_BackToTheCurrentPage_WithAForcedReload()
    {
        var page = new FakeNavigation("https://localhost/settings");
        var service = CreateService(page);
        var seen = Observe(service);

        await service.SetCultureAsync(new CultureInfo("de"));

        var navigated = page.Navigations.Single();
        await Assert.That(navigated.Uri).IsEqualTo("/culture/set?culture=de&redirectUri=%2Fsettings");
        await Assert.That(navigated.ForceLoad).IsTrue();

        // The reloaded page starts with the new culture; nothing changes in this circuit.
        await Assert.That(seen).Count().IsEqualTo(1);
        await Assert.That(js.Calls).IsEmpty();
    }

    [Test]
    public async Task TheRedirectKeepsTheWholePath_IncludingASelectedArchive()
    {
        var page = new FakeNavigation("https://localhost/users/42/archives/7");
        var service = CreateService(page);

        await service.SetCultureAsync(new CultureInfo("en"));

        await Assert
            .That(page.Navigations.Single().Uri)
            .IsEqualTo("/culture/set?culture=en&redirectUri=%2Fusers%2F42%2Farchives%2F7");
    }

    [Test]
    public async Task DisposingTheService_CompletesTheStream()
    {
        var service = CreateService();
        var completed = false;
        service.Settings.Subscribe(_ => { }, () => completed = true);

        await service.DisposeAsync();

        await Assert.That(completed).IsTrue();
    }

    [Test]
    public async Task DisposingTwice_IsHarmless_AsWhenTheContainerDisposesItOnceForEachRegistration()
    {
        var service = CreateService();

        await service.DisposeAsync();

        await Assert.That(async () => await service.DisposeAsync()).ThrowsNothing();
    }

    private sealed class FakeJs : IJSRuntime
    {
        public FakeModule Module { get; } = new();
        public List<string> ImportedModules { get; } = [];
        public List<string> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add(identifier);

            if (identifier == "import")
            {
                ImportedModules.Add((string)args![0]!);
            }

            return new ValueTask<TValue>((TValue)(object)Module);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args
        ) => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class FakeModule : IJSObjectReference
    {
        public List<(string Identifier, object?[]? Args)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args));

            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args
        ) => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => default;
    }

    private sealed class FakeNavigation : NavigationManager
    {
        public FakeNavigation(string uri) => Initialize("https://localhost/", uri);

        public List<(string Uri, bool ForceLoad)> Navigations { get; } = [];

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            Navigations.Add((uri, options.ForceLoad));
    }
}
