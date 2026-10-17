using OfficeRkyv.UI.Localization;
using OfficeRkyv.UI.Shared;
using Radzen;

namespace OfficeRkyv.UI;

public static class OfficeRkyvUiExtensions
{
    /// <summary>
    /// What the shared UI needs from its host's container: Radzen, localization from the library's resources,
    /// authorization of pages and the signed-in user as a cascaded value, and the facades. The host adds the
    /// rest: DotNetQuery (its options differ per host), the gRPC clients (their addresses differ, and each gets
    /// <see cref="Auth.CallerTokenCredentials"/>), the user settings, and who is signed in
    /// (an <c>AuthenticationStateProvider</c> and an <see cref="Auth.ISignInService"/>).
    /// </summary>
    public static IServiceCollection AddOfficeRkyvUi(this IServiceCollection services)
    {
        services.AddRadzenComponents();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddLocalization(options => options.ResourcesPath = SupportedCultures.ResourcesPath);
        services.AddScoped<UsersFacade>();
        services.AddScoped<ArchivesFacade>();
        services.AddScoped<DocumentsFacade>();

        return services;
    }
}
