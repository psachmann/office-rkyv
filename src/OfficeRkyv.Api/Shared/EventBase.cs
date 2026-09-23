namespace OfficeRkyv.Api.Shared;

public abstract record EventBase<TPayload> : IEvent<TPayload>
    where TPayload : class
{
    public required Guid Id { get; init; }

    public required Guid EntityId { get; init; }

    public required int Version { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required TPayload Payload { get; init; }
}
