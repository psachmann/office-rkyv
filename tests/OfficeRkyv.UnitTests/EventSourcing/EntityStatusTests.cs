using OfficeRkyv.Api.Shared;
using OfficeRkyv.UnitTests.EventSourcing.Fixtures;

namespace OfficeRkyv.UnitTests.EventSourcing;

public class EntityStatusTests
{
    [Test]
    public async Task NewEntity_WithoutBeingProjected_HasUnknownStatus()
    {
        var entity = new TestEntity { Id = Guid.NewGuid() };

        await Assert.That(entity.Status).IsEqualTo(EntityStatus.Unknown);
    }
}
