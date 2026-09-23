namespace OfficeRkyv.Api.Shared;

public interface IEvent<TPayload>
    where TPayload : class
{
    public Guid Id { get; init; }

    public Guid EntityId { get; init; }

    public int Version { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public TPayload Payload { get; init; }
}
