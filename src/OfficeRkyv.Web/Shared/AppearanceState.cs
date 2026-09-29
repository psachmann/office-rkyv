using Microsoft.JSInterop;

namespace OfficeRkyv.Web.Shared;

/// <summary>
/// The appearance preference of one circuit. The settings page shows and changes it. A change is applied to
/// the theme links the page is already showing, and is written to the preference cookie so that the next page
/// load starts with it.
/// </summary>
public sealed class AppearanceState(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? module;

    public AppearancePreference Preference { get; private set; }

    /// <summary>Raised whenever <see cref="Preference"/> changes, whoever changed it.</summary>
    public event Action? Changed;

    /// <summary>
    /// Adopts the preference the page was rendered with (read from the cookie on the server), without
    /// writing it back: it came from there.
    /// </summary>
    public void Initialize(AppearancePreference preference)
    {
        if (Preference != preference)
        {
            Preference = preference;
            Changed?.Invoke();
        }
    }

    public async Task SetAsync(AppearancePreference preference)
    {
        if (Preference == preference)
        {
            return;
        }

        // Shown at once by whoever listens; applying it to the page and remembering it follows.
        Preference = preference;
        Changed?.Invoke();

        module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/appearance.js");
        await module.InvokeVoidAsync(
            "applyAppearance",
            Appearance.CookieName,
            Appearance.ToCookieValue(preference),
            Appearance.Stylesheets(preference)
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            await module.DisposeAsync();
        }
    }
}
