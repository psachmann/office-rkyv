namespace OfficeRkyv.Api.Shared;

public abstract record EntityBase : IEntity
{
    [JsonInclude]
    public Guid Id { get; protected set; }

    [JsonInclude]
    public int Version { get; protected set; }

    [JsonInclude]
    public EntityStatus Status { get; protected set; }

    [JsonInclude]
    public DateTimeOffset CreatedAt { get; protected set; }

    [JsonInclude]
    public DateTimeOffset UpdatedAt { get; protected set; }
}
