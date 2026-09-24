using Microsoft.EntityFrameworkCore;

namespace OfficeRkyv.Api.Data;

public interface IApplyV2<TEntity, TEvent, TPayload>
    where TEntity : class, IEntity
    where TEvent : class, IEvent<TPayload>
    where TPayload : class
{
    public TEntity Apply(TEntity entity, TEvent @event);
}

internal sealed class EventStoreImpl<TEntity, TProjector>(OfficeRkyvContext context, TProjector projector)
    where TEntity : class, IEntity
    where TProjector : class
{
    public IQueryable<EventEntity> Events => context.Set<EventEntity>().AsQueryable().AsNoTracking();

    public Task CreateAsync<TEvent, TPayload>(TEvent @event, CancellationToken cancellationToken)
        where TProjector : IApply<TEntity, TPayload>
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
