using OfficeRkyv.UI.Localization;
using OfficeRkyv.UI.Shared;
using Radzen;

namespace OfficeRkyv.UI;

public static class OfficeRkyvUiExtensions
{
    /// <summary>
    /// What the shared UI needs from its host's container: Radzen, localization from the library's resources,
    /// and the facades. The host adds the rest: DotNetQuery (its options differ per host), the gRPC clients
    /// (their addresses differ), and the user settings.
    /// </summary>
    public static IServiceCollection AddOfficeRkyvUi(this IServiceCollection services)
    {
        services.AddRadzenComponents();
        services.AddLocalization(options => options.ResourcesPath = SupportedCultures.ResourcesPath);
        services.AddScoped<UsersFacade>();
        services.AddScoped<ArchivesFacade>();
        services.AddScoped<DocumentsFacade>();

        return services;
    }
}
