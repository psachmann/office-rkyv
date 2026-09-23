using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OfficeRkyv.Api.Data;

/// <summary>
/// Append-only, versioned event log. Depends on <see cref="DbContext"/> rather than the concrete
/// <see cref="OfficeRkyvContext"/> so it can append to and replay from any context an entity's
/// events are mapped into - including a test-local context that maps a test-only entity.
/// </summary>
internal sealed class EventStore(DbContext context)
{
    public async Task<IReadOnlyList<IEvent>> AppendAsync(
        Guid entityId,
        int expectedVersion,
        IReadOnlyList<object> payloads,
        CancellationToken cancellationToken = default
    )
    {
        var events = await StageAsync(entityId, expectedVersion, payloads, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new EventStoreConcurrencyException(entityId, expectedVersion, exception);
        }

        return events;
    }

    /// <summary>
    /// Stages the version check and event inserts on the change tracker without saving, so a caller
    /// that also needs to update materialized state (see <see cref="MaterializedEntityStore{TEntity,TProjector}"/>)
    /// can commit both in a single transaction via one <see cref="DbContext.SaveChangesAsync"/> call.
    /// </summary>
    internal async Task<IReadOnlyList<IEvent>> StageAsync(
        Guid entityId,
        int expectedVersion,
        IReadOnlyList<object> payloads,
        CancellationToken cancellationToken = default
    )
    {
        var currentVersion = await GetCurrentVersionAsync(entityId, cancellationToken);

        if (currentVersion != expectedVersion)
        {
            throw new EventStoreConcurrencyException(entityId, expectedVersion, currentVersion);
        }

        var timestamp = DateTimeOffset.UtcNow;
        var events = new List<IEvent>(payloads.Count);
        var version = expectedVersion;

        foreach (var payload in payloads)
        {
            version++;

            var @event = new Event
            {
                Id = Guid.NewGuid(),
                EntityId = entityId,
                Version = version,
                Timestamp = timestamp,
                Payload = payload,
            };

            events.Add(@event);

            context
                .Set<EventRecord>()
                .Add(
                    new EventRecord
                    {
                        Id = @event.Id,
                        EntityId = entityId,
                        Version = version,
                        Timestamp = timestamp,
                        EventType = EventTypeName.For(payload.GetType()),
                        PayloadJson = JsonSerializer.Serialize(payload, payload.GetType()),
                    }
                );
        }

        return events;
    }

    public async Task<IReadOnlyList<IEvent>> GetByEntityIdAsync(
        Guid entityId,
        CancellationToken cancellationToken = default
    )
    {
        var records = await context
            .Set<EventRecord>()
            .Where(record => record.EntityId == entityId)
            .OrderBy(record => record.Version)
            .ToListAsync(cancellationToken);

        return records
            .Select(record =>
            {
                var payloadType = EventTypeName.Resolve(record.EventType);

                return (IEvent)
                    new Event
                    {
                        Id = record.Id,
                        EntityId = record.EntityId,
                        Version = record.Version,
                        Timestamp = record.Timestamp,
                        Payload = JsonSerializer.Deserialize(record.PayloadJson, payloadType)!,
                    };
            })
            .ToList();
    }

    private async Task<int> GetCurrentVersionAsync(Guid entityId, CancellationToken cancellationToken) =>
        await context
            .Set<EventRecord>()
            .Where(record => record.EntityId == entityId)
            .Select(record => (int?)record.Version)
            .OrderByDescending(version => version)
            .FirstOrDefaultAsync(cancellationToken)
        ?? 0;
}
