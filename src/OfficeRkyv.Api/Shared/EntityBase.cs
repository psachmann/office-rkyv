namespace OfficeRkyv.Api.Shared;

public abstract record EntityBase : IEntity
{
    public Guid Id { get; protected set; }

    public int Version { get; protected set; }

    public EntityStatus Status { get; protected set; }

    public DateTimeOffset CreatedAt { get; protected set; }

    public DateTimeOffset UpdatedAt { get; protected set; }
}
