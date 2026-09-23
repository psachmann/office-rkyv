using Fisher;
using JasperFx.Events;
using JasperFx.Events.Documents;
using JasperFx.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeRkyv.Api;
using OfficeRkyv.Api.Features.Users;
using Wolverine;
using Wolverine.Fisher;

namespace OfficeRkyv.UnitTests.Users;

/// <summary>
/// Drives the real Wolverine pipeline - decider middleware, concurrency guard and all - against a
/// real Fisher store, so what these tests exercise is what the gRPC services call.
/// </summary>
public sealed class UserPipelineTests : IAsyncDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.db");
    private IHost host = null!;

    private IMessageBus Bus => host.Services.GetRequiredService<IMessageBus>();

    [Before(Test)]
    public async Task SetUp() =>
        host = await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
                services
                    .AddFisher(options =>
                    {
                        options.Connection($"Data Source={path}");
                        options.Projections.Snapshot<User>(SnapshotLifecycle.Inline);
                    })
                    .ApplyAllDatabaseChangesOnStartup()
                    .IntegrateWithWolverine()
            )
            .UseWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;
                options.Policies.AutoApplyTransactions();
                options.Discovery.IncludeAssembly(OfficeRkyvApiLib.Assembly);
            })
            .StartAsync();

    [Test]
    public async Task CreateThenQueryImmediately_SeesTheWrite()
    {
        await Bus.InvokeAsync<User>(new CreateUserCommand("ada@example.com", "Ada Lovelace"));

        // No daemon, no polling, no checkpoint wait - the snapshot was written in the same
        // transaction as the event append.
        var user = await Bus.InvokeAsync<User?>(new FindUserByEmailQuery("ada@example.com"));

        await Assert.That(user).IsNotNull();
        await Assert.That(user!.DisplayName).IsEqualTo("Ada Lovelace");
        await Assert.That(user.Version).IsEqualTo(1);
    }

    [Test]
    public async Task FullLifecycle_CreateRenameDelete_LeavesThreeEventsAndASoftDeletedDocument()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("grace@example.com", "Grace H"));

        await Bus.InvokeAsync(new UpdateUserCommand(created.Id, created.Version, "Grace Hopper"));

        var renamed = await Bus.InvokeAsync<User?>(new FindUserByIdQuery(created.Id));
        await Assert.That(renamed!.DisplayName).IsEqualTo("Grace Hopper");

        await Bus.InvokeAsync(new DeleteUserCommand(created.Id, renamed.Version));

        var remaining = await Bus.InvokeAsync<IReadOnlyList<User>>(new FindAllUsersQuery());
        await Assert.That(remaining.Any(user => user.Id == created.Id)).IsFalse();

        await using var session = host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        var document = await session.LoadAsync<User>(created.Id);
        await Assert.That(document).IsNotNull();
        await Assert.That(document!.IsDeleted).IsTrue();

        var events = await session.Events.FetchStreamAsync(created.Id);
        await Assert
            .That(events.Select(@event => @event.Data.GetType().Name))
            .IsEquivalentTo([nameof(UserCreated), nameof(UserRenamed), nameof(UserDeleted)]);
    }

    [Test]
    public async Task UpdateWithNoActualChange_AppendsNothing()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("alan@example.com", "Alan Turing"));

        await Bus.InvokeAsync(new UpdateUserCommand(created.Id, created.Version, "Alan Turing"));

        await using var session = host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        var events = await session.Events.FetchStreamAsync(created.Id);

        await Assert.That(events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DuplicateEmail_IsRefusedByTheStore_NotByAHandlerPreCheck()
    {
        await Bus.InvokeAsync<User>(new CreateUserCommand("dup@example.com", "First"));

        // No AnyAsync check anywhere in CreateUserHandler - the natural key is claimed inside the
        // append transaction, so this is a real constraint rather than a check-then-write race.
        await Assert
            .That(async () => await Bus.InvokeAsync<User>(new CreateUserCommand("dup@example.com", "Second")))
            .Throws<DuplicateNaturalKeyException>();
    }

    [Test]
    public async Task StaleExpectedVersion_IsRejected()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("edsger@example.com", "Edsger D"));

        await Bus.InvokeAsync(new UpdateUserCommand(created.Id, created.Version, "Edsger Dijkstra"));

        // Version 1 is stale now - the rename moved the stream to 2. Optimistic concurrency reaches
        // all the way out to the caller because the command carries the version it read.
        await Assert
            .That(async () => await Bus.InvokeAsync(new UpdateUserCommand(created.Id, created.Version, "Someone Else")))
            .Throws<EventStreamUnexpectedMaxEventIdException>();
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync();
        host.Dispose();
        File.Delete(path);
    }
}
