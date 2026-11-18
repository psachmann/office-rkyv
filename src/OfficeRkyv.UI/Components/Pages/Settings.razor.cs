using System.Globalization;
using Microsoft.AspNetCore.Components;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UI.Components.Pages;

public partial class Settings
{
    private IDisposable? subscription;

    // Stays in step when the settings change elsewhere, or are only adopted after this page first rendered.
    // The stream replays the settings in force, so the first emission is a harmless extra render.
    protected override void OnInitialized() =>
        subscription = SettingsService.Settings.Subscribe(current => _ = InvokeAsync(StateHasChanged));

    private Task ChangeCulture(string cultureName) =>
        SettingsService.SetCultureAsync(CultureInfo.GetCultureInfo(cultureName));

    private Task ChangeAppearance(AppearancePreference appearance) => SettingsService.SetAppearanceAsync(appearance);

    public void Dispose() => subscription?.Dispose();
}
