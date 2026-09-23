using Fisher;
using JasperFx.Events;
using JasperFx.Events.Documents;
using JasperFx.Events.Projections;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Features.Users.Handlers;

namespace OfficeRkyv.UnitTests.Fisher;

/// <summary>
/// The whole point of the spike: drive the real Mediator handlers against a real Fisher store and
/// prove the property this project actually needs - a query issued immediately after a command sees
/// the write, with no daemon running and no waiting.
/// </summary>
public sealed class FisherUserPipelineTests : IAsyncDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.db");
    private DocumentStore store = null!;

    [Before(Test)]
    public async Task SetUp()
    {
        store = DocumentStore.For(options =>
        {
            options.Connection($"Data Source={path}");
            options.Projections.Snapshot<UserEntity>(SnapshotLifecycle.Inline);
        });

        await store.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    [Test]
    public async Task CreateThenQueryImmediately_SeesTheWrite()
    {
        await using var session = store.LightweightSession();

        await new CreateUserHandler(session).Handle(
            new CreateUserCommand("ada@example.com", "Ada Lovelace"),
            CancellationToken.None
        );

        // No await on a daemon, no retry loop, no polling a checkpoint - the snapshot was written in
        // the same transaction as the event append.
        var user = await new FindUserByEmailHandler(session).Handle(
            new FindUserByEmailQuery("ada@example.com"),
            CancellationToken.None
        );

        await Assert.That(user).IsNotNull();
        await Assert.That(user!.DisplayName).IsEqualTo("Ada Lovelace");
        await Assert.That(user.CreatedAt).IsNotEqualTo(default(DateTimeOffset));
    }

    [Test]
    public async Task FullLifecycle_CreateRenameDelete_LeavesThreeEventsAndASoftDeletedDocument()
    {
        await using var session = store.LightweightSession();
        var create = new CreateUserHandler(session);
        var update = new UpdateUserHandler(session);
        var delete = new DeleteUserHandler(session);

        await create.Handle(new CreateUserCommand("grace@example.com", "Grace H"), CancellationToken.None);
        await update.Handle(new UpdateUserCommand("grace@example.com", "Grace Hopper"), CancellationToken.None);

        var renamed = await new FindUserByEmailHandler(session).Handle(
            new FindUserByEmailQuery("grace@example.com"),
            CancellationToken.None
        );
        await Assert.That(renamed!.DisplayName).IsEqualTo("Grace Hopper");

        await delete.Handle(new DeleteUserCommand("grace@example.com"), CancellationToken.None);

        var afterDelete = await new FindAllUsersHandler(session).Handle(
            new FindAllUsersQuery(),
            CancellationToken.None
        );
        await Assert.That(afterDelete.Any(user => user.Email == "grace@example.com")).IsFalse();

        // Soft delete: the document is filtered by the query, not removed, and the history is intact.
        var document = await session.Query<UserEntity>().Where(user => user.Email == "grace@example.com").ToListAsync();
        await Assert.That(document.Count).IsEqualTo(1);
        await Assert.That(document[0].IsDeleted).IsTrue();

        var events = await session.Events.FetchStreamAsync(document[0].Id);
        await Assert.That(events.Count).IsEqualTo(3);
        await Assert
            .That(events.Select(@event => @event.Data.GetType().Name))
            .IsEquivalentTo([nameof(UserCreated), nameof(UserRenamed), nameof(UserDeleted)]);
    }

    [Test]
    public async Task UpdateWithNoActualChange_AppendsNothing()
    {
        await using var session = store.LightweightSession();

        await new CreateUserHandler(session).Handle(
            new CreateUserCommand("alan@example.com", "Alan Turing"),
            CancellationToken.None
        );
        await new UpdateUserHandler(session).Handle(
            new UpdateUserCommand("alan@example.com", "Alan Turing"),
            CancellationToken.None
        );

        var user = await new FindUserByEmailHandler(session).Handle(
            new FindUserByEmailQuery("alan@example.com"),
            CancellationToken.None
        );
        var events = await session.Events.FetchStreamAsync(user!.Id);

        await Assert.That(events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DuplicateEmail_IsRefusedByTheStore_NotByAHandlerPreCheck()
    {
        await using var session = store.LightweightSession();
        var create = new CreateUserHandler(session);

        await create.Handle(new CreateUserCommand("dup@example.com", "First"), CancellationToken.None);

        // No AnyAsync check anywhere in CreateUserHandler - the natural key is claimed inside the
        // append transaction, so this is a real constraint rather than a check-then-write race.
        await Assert
            .That(async () =>
                await create.Handle(new CreateUserCommand("dup@example.com", "Second"), CancellationToken.None)
            )
            .Throws<DuplicateNaturalKeyException>();
    }

    public async ValueTask DisposeAsync()
    {
        await store.DisposeAsync();
        File.Delete(path);
    }
}
