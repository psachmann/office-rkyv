using Microsoft.AspNetCore.Components;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UI.Components.Layout;

public partial class AppTheme
{
    private static readonly Version RadzenVersion = typeof(Radzen.Colors).Assembly.GetName().Version!;

    /// <summary>What the page is rendered with, read from the appearance cookie on the server.</summary>
    [Parameter]
    public AppearancePreference Preference { get; set; }

    private static string Href(ThemeStylesheet sheet) =>
        $"_content/Radzen.Blazor/css/{sheet.Theme}-base.css?v={RadzenVersion}";
}
