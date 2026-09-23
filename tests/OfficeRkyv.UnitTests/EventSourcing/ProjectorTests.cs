using OfficeRkyv.Api.Data;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.UnitTests.EventSourcing.Fixtures;

namespace OfficeRkyv.UnitTests.EventSourcing;

public class ProjectorTests
{
    [Test]
    public async Task Fold_WithMixedEventTypes_AppliesEachInOrderAndStampsVersion()
    {
        var entityId = Guid.NewGuid();
        var seed = new TestEntity { Id = entityId };
        var now = DateTimeOffset.UtcNow;

        IEvent[] events =
        [
            new Event
            {
                Id = Guid.NewGuid(),
                EntityId = entityId,
                Version = 1,
                Timestamp = now,
                Payload = new TestCreated("first"),
            },
            new Event
            {
                Id = Guid.NewGuid(),
                EntityId = entityId,
                Version = 2,
                Timestamp = now,
                Payload = new TestRenamed("second"),
            },
        ];

        var result = Projector<TestEntity, TestProjector>.Fold(seed, events);

        await Assert.That(result.Name).IsEqualTo("second");
        await Assert.That(result.Status).IsEqualTo(EntityStatus.Active);
        await Assert.That(result.Version).IsEqualTo(2);
    }
}
