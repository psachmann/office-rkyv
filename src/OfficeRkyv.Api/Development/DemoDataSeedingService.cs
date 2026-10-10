using Wolverine;

namespace OfficeRkyv.Api.Development;

/// <summary>
/// Runs the seed once after the host started, off the startup path, so the API serves requests while the five
/// commands run. A scope is created because the bus is not guaranteed to be resolvable from the root provider
/// when scope validation is on, as it is in Development.
/// </summary>
internal sealed class DemoDataSeedingService(IServiceProvider services, DemoDataSeeder seeder) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = services.CreateAsyncScope();

        await seeder.SeedAsync(scope.ServiceProvider.GetRequiredService<IMessageBus>(), stoppingToken);
    }
}
