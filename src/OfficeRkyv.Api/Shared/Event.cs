namespace OfficeRkyv.Api.Shared;

public record Event<TPayload> : IEvent<TPayload>
    where TPayload : class
{
    public Guid Id { get; init; }

    public Guid EntityId { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public required TPayload Payload { get; init; }
}
