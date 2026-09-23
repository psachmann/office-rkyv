using OfficeRkyv.Api.Data;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.UnitTests.EventSourcing.Fixtures;

namespace OfficeRkyv.UnitTests.EventSourcing;

public class SoftDeleteTests
{
    [Test]
    public async Task HandleAsync_WithDeleteCommand_MarksStatusDeletedAndKeepsRow()
    {
        await using var db = await TestDatabase.CreateAsync();
        var eventStore = new EventStore(db.Context);
        var store = new MaterializedEntityStore<TestEntity, TestProjector>(db.Context, eventStore);
        var entityId = Guid.NewGuid();

        await store.HandleAsync<CreateCommand, CreateDecider>(entityId, new CreateCommand("first"));
        await store.HandleAsync<DeleteCommand, DeleteDecider>(entityId, new DeleteCommand());

        var materialized = await store.GetAsync(entityId);

        await Assert.That(materialized).IsNotNull();
        await Assert.That(materialized!.Status).IsEqualTo(EntityStatus.Deleted);
        await Assert.That(materialized.Name).IsEqualTo("first");
    }

    [Test]
    public async Task GetByEntityIdAsync_AfterDeletion_ReturnsFullHistoryIncludingDeletion()
    {
        await using var db = await TestDatabase.CreateAsync();
        var eventStore = new EventStore(db.Context);
        var store = new MaterializedEntityStore<TestEntity, TestProjector>(db.Context, eventStore);
        var entityId = Guid.NewGuid();

        await store.HandleAsync<CreateCommand, CreateDecider>(entityId, new CreateCommand("first"));
        await store.HandleAsync<DeleteCommand, DeleteDecider>(entityId, new DeleteCommand());

        var history = await eventStore.GetByEntityIdAsync(entityId);

        await Assert.That(history.Count).IsEqualTo(2);
        await Assert.That(history[0].Payload).IsTypeOf<TestCreated>();
        await Assert.That(history[1].Payload).IsTypeOf<Deleted>();
    }
}
