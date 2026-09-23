namespace OfficeRkyv.Api.Shared;

public abstract record EntityBase : IEntity
{
    public Guid Id { get; init; }

    public int Version { get; init; }

    public EntityStatus Status { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    // Applied by the projector for every Deleted event, regardless of entity type - soft delete is
    // infrastructure-owned, not something individual entity projectors implement.
    internal EntityBase MarkDeleted(DateTimeOffset timestamp) =>
        this with
        {
            Status = EntityStatus.Deleted,
            UpdatedAt = timestamp,
        };

    // Applied by the projector after folding every event - individual projectors never set this.
    internal EntityBase WithVersion(int version) => this with { Version = version };
}
