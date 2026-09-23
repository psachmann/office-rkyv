using Microsoft.EntityFrameworkCore;

namespace OfficeRkyv.Api.Data;

internal sealed class EventStoreImpl(OfficeRkyvContext context)
{
    public IQueryable<EventEntity> Events => context.Set<EventEntity>().AsQueryable().AsNoTracking();

    public Task CreateAsync<TEvent, TPayload>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent<TPayload>
        where TPayload : class
    {
        throw new NotImplementedException();
    }

    public Task AppendAsync<TEvent, TPayload>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent<TPayload>
        where TPayload : class
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync<TEvent, TPayload>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent<TPayload>
        where TPayload : class
    {
        throw new NotImplementedException();
    }
}
