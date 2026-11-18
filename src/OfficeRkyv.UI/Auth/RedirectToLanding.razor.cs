namespace OfficeRkyv.UI.Auth;

public partial class RedirectToLanding
{
    protected override void OnInitialized() => Navigation.NavigateTo("/", replace: true);
}
