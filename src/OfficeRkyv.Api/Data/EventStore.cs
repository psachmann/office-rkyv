using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OfficeRkyv.Api.Data;

internal sealed class EventStore(OfficeRkyvContext context)
{
    public async Task AppendAsync<TPayload>(Event<TPayload> @event, CancellationToken cancellationToken = default)
        where TPayload : class
    {
        context.Events.Add(
            new EventRecord
            {
                Id = @event.Id,
                EntityId = @event.EntityId,
                Timestamp = @event.Timestamp,
                EventType = EventTypeName.For<TPayload>(),
                PayloadJson = JsonSerializer.Serialize(@event.Payload),
            }
        );

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Event<TPayload>>> GetByEntityIdAsync<TPayload>(
        Guid entityId,
        CancellationToken cancellationToken = default
    )
        where TPayload : class
    {
        var eventType = EventTypeName.For<TPayload>();

        var records = await context
            .Events.Where(record => record.EntityId == entityId && record.EventType == eventType)
            .OrderBy(record => record.Timestamp)
            .ToListAsync(cancellationToken);

        return records
            .Select(record => new Event<TPayload>
            {
                Id = record.Id,
                EntityId = record.EntityId,
                Timestamp = record.Timestamp,
                Payload = JsonSerializer.Deserialize<TPayload>(record.PayloadJson)!,
            })
            .ToList();
    }
}
