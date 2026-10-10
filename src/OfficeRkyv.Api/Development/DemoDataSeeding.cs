using Microsoft.AspNetCore.Builder;

namespace OfficeRkyv.Api.Development;

/// <summary>
/// The development seed of the API server. It is not part of <c>AddOfficeRkyvApi</c> on purpose: that
/// composition is shared with the desktop app, whose data lives in the user's real data directory, and with
/// the tests. Only a host that calls <see cref="AddDevelopmentSeedData"/> seeds.
/// </summary>
public static class DemoDataSeeding
{
    /// <summary>Configuration key that switches the seed off with <c>false</c>; absent means on.</summary>
    public const string SettingName = "Seed:DemoData";

    /// <summary>
    /// Outside Development the answer is always no, so a setting left switched on in a deployment cannot seed.
    /// </summary>
    public static bool IsEnabled(bool isDevelopment, bool? setting) => isDevelopment && setting != false;

    /// <summary>
    /// Registers the seed when <see cref="IsEnabled"/> says so. Call it after <c>AddOfficeRkyvApi</c>: the
    /// seed starts after the store and Wolverine, in registration order.
    /// </summary>
    public static WebApplicationBuilder AddDevelopmentSeedData(this WebApplicationBuilder builder)
    {
        // A value that is not a boolean counts as absent, so a typo cannot switch the seed off by accident.
        bool? setting = bool.TryParse(builder.Configuration[SettingName], out var parsed) ? parsed : null;

        if (!IsEnabled(builder.Environment.IsDevelopment(), setting))
        {
            return builder;
        }

        builder.Services.AddSingleton(DemoData.Default);
        builder.Services.AddSingleton<DemoDataSeeder>();
        builder.Services.AddHostedService<DemoDataSeedingService>();

        return builder;
    }
}
