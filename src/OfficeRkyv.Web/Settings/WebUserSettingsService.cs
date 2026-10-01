using System.Globalization;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OfficeRkyv.UI.Localization;
using OfficeRkyv.UI.Settings;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.Web.Settings;

/// <summary>
/// The web host's settings, one per circuit. The appearance lives in a cookie: the server reads it for the
/// first paint (see <c>App.razor</c>), and a change is applied to the page and written back through JS. The
/// language lives in the culture cookie, which only a fresh circuit honours, so changing it reloads the page
/// through <c>/culture/set</c> (see docs/ui.md).
/// </summary>
public sealed class WebUserSettingsService(IJSRuntime js, NavigationManager navigation)
    : IUserSettingsService,
        IAsyncDisposable
{
    private readonly BehaviorSubject<UserSettings> settings = new(
        new UserSettings(AppearancePreference.System, SupportedCultures.Default)
    );
    private IJSObjectReference? module;
    private int disposed;

    public UserSettings Current => settings.Value;

    public IObservable<UserSettings> Settings => settings;

    /// <summary>
    /// Adopts what the page was rendered with: the appearance read from the cookie on the server, and the
    /// culture of the request. Nothing is written back; it came from there.
    /// </summary>
    public void Initialize(AppearancePreference appearance, CultureInfo culture) =>
        Publish(new UserSettings(appearance, SupportedCultures.Match(culture)));

    public async Task SetAppearanceAsync(AppearancePreference appearance)
    {
        if (Current.Appearance == appearance)
        {
            return;
        }

        // Shown at once by whoever listens; applying it to the page and remembering it follows.
        Publish(Current with { Appearance = appearance });

        module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/OfficeRkyv.UI/js/appearance.js");
        await module.InvokeVoidAsync(
            "applyAppearance",
            Appearance.CookieName,
            Appearance.ToCookieValue(appearance),
            Appearance.Stylesheets(appearance)
        );
    }

    public Task SetCultureAsync(CultureInfo culture)
    {
        // A culture only takes effect on a fresh circuit, so this is a real navigation that lands back on
        // the page the language was changed on. Current does not change: the reloaded page starts with it.
        var relativePath = navigation.ToBaseRelativePath(navigation.Uri);
        var redirectUri = Uri.EscapeDataString("/" + relativePath);

        navigation.NavigateTo($"/culture/set?culture={culture.Name}&redirectUri={redirectUri}", forceLoad: true);

        return Task.CompletedTask;
    }

    private void Publish(UserSettings next)
    {
        if (next != Current)
        {
            settings.OnNext(next);
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Registered as itself and as IUserSettingsService, so the container disposes it once for each.
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }

        settings.OnCompleted();
        settings.Dispose();

        if (module is not null)
        {
            await module.DisposeAsync();
        }
    }
}
