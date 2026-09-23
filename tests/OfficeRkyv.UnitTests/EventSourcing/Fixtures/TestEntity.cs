using OfficeRkyv.Api.Shared;

namespace OfficeRkyv.UnitTests.EventSourcing.Fixtures;

internal sealed record TestEntity : EntityBase
{
    public string Name { get; init; } = "";
}
