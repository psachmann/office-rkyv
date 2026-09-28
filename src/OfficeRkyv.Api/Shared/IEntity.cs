namespace OfficeRkyv.Api.Shared;

public interface IEntity
{
    public Guid Id { get; }

    public int Version { get; }

    public EntityStatus Status { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }
}
