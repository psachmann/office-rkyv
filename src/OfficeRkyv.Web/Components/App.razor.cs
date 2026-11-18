using Microsoft.AspNetCore.Components;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.Web.Components;

public partial class App
{
    // The appearance the visitor chose, from the cookie the settings page writes. Read here, on the server,
    // so that the page is rendered in it from its first paint.
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    private AppearancePreference preference => Appearance.Parse(HttpContext?.Request.Cookies[Appearance.CookieName]);
}
