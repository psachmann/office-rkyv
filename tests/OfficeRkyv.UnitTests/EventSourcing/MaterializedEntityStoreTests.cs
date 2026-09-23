using OfficeRkyv.Api.Data;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.UnitTests.EventSourcing.Fixtures;

namespace OfficeRkyv.UnitTests.EventSourcing;

public class MaterializedEntityStoreTests
{
    [Test]
    public async Task AppendAsync_ReadImmediatelyAfter_ReflectsTheAppendedEvent()
    {
        await using var db = await TestDatabase.CreateAsync();
        var eventStore = new EventStore(db.Context);
        var store = new MaterializedEntityStore<TestEntity, TestProjector>(db.Context, eventStore);
        var entityId = Guid.NewGuid();

        await store.AppendAsync(entityId, 0, [new TestCreated("first")]);

        var materialized = await store.GetAsync(entityId);

        await Assert.That(materialized).IsNotNull();
        await Assert.That(materialized!.Name).IsEqualTo("first");
        await Assert.That(materialized.Status).IsEqualTo(EntityStatus.Active);
        await Assert.That(materialized.Version).IsEqualTo(1);
    }

    [Test]
    public async Task HandleAsync_ReadsMaterializedStateAndProducesSameResultAsFullReplay()
    {
        await using var db = await TestDatabase.CreateAsync();
        var eventStore = new EventStore(db.Context);
        var store = new MaterializedEntityStore<TestEntity, TestProjector>(db.Context, eventStore);
        var entityId = Guid.NewGuid();

        await store.HandleAsync<CreateCommand, CreateDecider>(entityId, new CreateCommand("first"));
        var afterRename = await store.HandleAsync<RenameCommand, RenameDecider>(entityId, new RenameCommand("second"));

        var replayed = Projector<TestEntity, TestProjector>.Fold(
            new TestEntity { Id = entityId },
            await eventStore.GetByEntityIdAsync(entityId)
        );

        await Assert.That(afterRename.Name).IsEqualTo(replayed.Name);
        await Assert.That(afterRename.Version).IsEqualTo(replayed.Version);
        await Assert.That(afterRename.Status).IsEqualTo(replayed.Status);
    }

    [Test]
    public async Task HandleAsync_WithRejectedCommand_LeavesMaterializedRowUnchanged()
    {
        await using var db = await TestDatabase.CreateAsync();
        var eventStore = new EventStore(db.Context);
        var store = new MaterializedEntityStore<TestEntity, TestProjector>(db.Context, eventStore);
        var entityId = Guid.NewGuid();

        await store.HandleAsync<CreateCommand, CreateDecider>(entityId, new CreateCommand("first"));

        await Assert.ThrowsAsync<InvariantViolationException>(async () =>
            await store.HandleAsync<RenameCommand, RenameDecider>(entityId, new RenameCommand(""))
        );

        var materialized = await store.GetAsync(entityId);
        await Assert.That(materialized!.Name).IsEqualTo("first");
        await Assert.That(materialized.Version).IsEqualTo(1);
    }

    [Test]
    public async Task AppendAsync_WithVersionConflict_LeavesMaterializedRowUnchanged()
    {
        await using var db = await TestDatabase.CreateAsync();
        var eventStore = new EventStore(db.Context);
        var store = new MaterializedEntityStore<TestEntity, TestProjector>(db.Context, eventStore);
        var entityId = Guid.NewGuid();

        await store.AppendAsync(entityId, 0, [new TestCreated("first")]);

        await Assert.ThrowsAsync<EventStoreConcurrencyException>(async () =>
            await store.AppendAsync(entityId, 0, [new TestRenamed("second")])
        );

        var materialized = await store.GetAsync(entityId);
        await Assert.That(materialized!.Name).IsEqualTo("first");
        await Assert.That(materialized.Version).IsEqualTo(1);
    }
}
