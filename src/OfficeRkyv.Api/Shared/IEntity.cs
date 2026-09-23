namespace OfficeRkyv.Api.Shared;

public interface IEntity
{
    public Guid Id { get; init; }

    public int Version { get; init; }

    public EntityStatus Status { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
