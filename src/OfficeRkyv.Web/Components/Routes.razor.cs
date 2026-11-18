using System.Globalization;
using Microsoft.AspNetCore.Components;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.Web.Components;

public partial class Routes
{
    /// <summary>
    /// The appearance the page was rendered with, read from the cookie on the server. With the culture of the
    /// request, it starts the circuit's settings, so the settings page shows the right choices however it is
    /// reached.
    /// </summary>
    [Parameter]
    public AppearancePreference Appearance { get; set; }

    protected override void OnInitialized() => SettingsService.Initialize(Appearance, CultureInfo.CurrentUICulture);
}
