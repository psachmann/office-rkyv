using OfficeRkyv.Api.Data;
using OfficeRkyv.UnitTests.EventSourcing.Fixtures;

namespace OfficeRkyv.UnitTests.EventSourcing;

public class EventStoreTests
{
    [Test]
    public async Task AppendAsync_WithMatchingExpectedVersion_AssignsSequentialVersions()
    {
        await using var db = await TestDatabase.CreateAsync();
        var store = new EventStore(db.Context);
        var entityId = Guid.NewGuid();

        var events = await store.AppendAsync(entityId, 0, [new TestCreated("first"), new TestRenamed("second")]);

        await Assert.That(events.Count).IsEqualTo(2);
        await Assert.That(events[0].Version).IsEqualTo(1);
        await Assert.That(events[1].Version).IsEqualTo(2);
    }

    [Test]
    public async Task AppendAsync_WithStaleExpectedVersion_ThrowsAndStoresNothing()
    {
        await using var db = await TestDatabase.CreateAsync();
        var store = new EventStore(db.Context);
        var entityId = Guid.NewGuid();

        await store.AppendAsync(entityId, 0, [new TestCreated("first")]);

        await Assert.ThrowsAsync<EventStoreConcurrencyException>(async () =>
            await store.AppendAsync(entityId, 0, [new TestRenamed("second")])
        );

        var history = await store.GetByEntityIdAsync(entityId);
        await Assert.That(history.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetByEntityIdAsync_WithMultipleEventTypes_ReturnsFullHistoryInVersionOrder()
    {
        await using var db = await TestDatabase.CreateAsync();
        var store = new EventStore(db.Context);
        var entityId = Guid.NewGuid();

        await store.AppendAsync(entityId, 0, [new TestCreated("first")]);
        await store.AppendAsync(entityId, 1, [new TestRenamed("second")]);

        var history = await store.GetByEntityIdAsync(entityId);

        await Assert.That(history.Count).IsEqualTo(2);
        await Assert.That(history[0].Payload).IsTypeOf<TestCreated>();
        await Assert.That(history[1].Payload).IsTypeOf<TestRenamed>();
        await Assert.That(history[0].Version).IsEqualTo(1);
        await Assert.That(history[1].Version).IsEqualTo(2);
    }
}
