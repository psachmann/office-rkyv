namespace OfficeRkyv.Api.Data;

internal sealed record EventRecord
{
    public Guid Id { get; init; }

    public Guid EntityId { get; init; }

    public int Version { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public required string EventType { get; init; }

    public required string PayloadJson { get; init; }
}
