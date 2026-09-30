using Microsoft.Maui.Controls;

namespace OfficeRkyv.Desktop;

public class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Here, and not earlier: the backend sets GTK's dark-theme preference from MAUI's own theme (always light)
        // while it builds the app, which would undo what is set before this point. The window, and so the
        // WebKit view, is created right after.
        DesktopHost.Current.StartAppearance();

        return new(new BlazorPage()) { Title = "Office Rkyv" };
    }
}
