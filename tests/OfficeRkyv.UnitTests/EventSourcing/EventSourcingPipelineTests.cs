using OfficeRkyv.Api.Data;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.UnitTests.EventSourcing.Fixtures;

namespace OfficeRkyv.UnitTests.EventSourcing;

/// <summary>
/// End-to-end proof of the append -> replay -> project -> decide -> materialize pipeline against a
/// real EF Core/SQLite-backed context, using a test-only entity/event/command fixture rather than
/// touching domain code (none exists yet).
/// </summary>
public class EventSourcingPipelineTests
{
    [Test]
    public async Task FullPipeline_CreateRenameDelete_ProducesConsistentReplayAndMaterializedState()
    {
        await using var db = await TestDatabase.CreateAsync();
        var eventStore = new EventStore(db.Context);
        var store = new MaterializedEntityStore<TestEntity, TestProjector>(db.Context, eventStore);
        var entityId = Guid.NewGuid();

        await store.HandleAsync<CreateCommand, CreateDecider>(entityId, new CreateCommand("first"));
        await store.HandleAsync<RenameCommand, RenameDecider>(entityId, new RenameCommand("second"));
        await store.HandleAsync<DeleteCommand, DeleteDecider>(entityId, new DeleteCommand());

        var materialized = await store.GetAsync(entityId);
        var history = await eventStore.GetByEntityIdAsync(entityId);
        var replayed = Projector<TestEntity, TestProjector>.Fold(new TestEntity { Id = entityId }, history);

        await Assert.That(history.Count).IsEqualTo(3);
        await Assert.That(materialized).IsNotNull();
        await Assert.That(materialized!.Name).IsEqualTo(replayed.Name);
        await Assert.That(materialized.Version).IsEqualTo(replayed.Version);
        await Assert.That(materialized.Status).IsEqualTo(EntityStatus.Deleted);
        await Assert.That(replayed.Status).IsEqualTo(EntityStatus.Deleted);
    }

    [Test]
    public async Task FullPipeline_RejectedCommand_NeitherAppendsNorMaterializes()
    {
        await using var db = await TestDatabase.CreateAsync();
        var eventStore = new EventStore(db.Context);
        var store = new MaterializedEntityStore<TestEntity, TestProjector>(db.Context, eventStore);
        var entityId = Guid.NewGuid();

        await store.HandleAsync<CreateCommand, CreateDecider>(entityId, new CreateCommand("first"));

        await Assert.ThrowsAsync<InvariantViolationException>(async () =>
            await store.HandleAsync<CreateCommand, CreateDecider>(entityId, new CreateCommand(" "))
        );

        var history = await eventStore.GetByEntityIdAsync(entityId);
        var materialized = await store.GetAsync(entityId);

        await Assert.That(history.Count).IsEqualTo(1);
        await Assert.That(materialized!.Name).IsEqualTo("first");
    }
}
