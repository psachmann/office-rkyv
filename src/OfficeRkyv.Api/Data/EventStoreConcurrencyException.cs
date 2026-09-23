namespace OfficeRkyv.Api.Data;

internal sealed class EventStoreConcurrencyException : Exception
{
    public EventStoreConcurrencyException(Guid entityId, int expectedVersion, int actualVersion)
        : base($"Entity '{entityId}' is at version {actualVersion}, but version {expectedVersion} was expected.")
    {
        EntityId = entityId;
        ExpectedVersion = expectedVersion;
    }

    public EventStoreConcurrencyException(Guid entityId, int expectedVersion, Exception innerException)
        : base(
            $"Appending to entity '{entityId}' at expected version {expectedVersion} failed a concurrency check.",
            innerException
        )
    {
        EntityId = entityId;
        ExpectedVersion = expectedVersion;
    }

    public Guid EntityId { get; }

    public int ExpectedVersion { get; }
}
