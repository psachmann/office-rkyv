using OfficeRkyv.Api.Shared;

namespace OfficeRkyv.UnitTests.EventSourcing.Fixtures;

internal sealed class TestProjector : IApply<TestEntity, TestCreated>, IApply<TestEntity, TestRenamed>
{
    public TestEntity Apply(TestEntity entity, TestCreated payload) =>
        entity with
        {
            Name = payload.Name,
            Status = EntityStatus.Active,
        };

    public TestEntity Apply(TestEntity entity, TestRenamed payload) => entity with { Name = payload.Name };
}
