namespace OfficeRkyv.Api.Shared;

public abstract record EntityBase : IEntity
{
    public Guid Id { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
