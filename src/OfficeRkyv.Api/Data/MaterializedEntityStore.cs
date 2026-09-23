using Microsoft.EntityFrameworkCore;

namespace OfficeRkyv.Api.Data;

/// <summary>
/// Reads and writes an entity's materialized row, keeping it synchronously consistent with the
/// event log: every append folds through <see cref="Projector{TEntity,TProjector}"/> and upserts
/// the result in the same <see cref="DbContext.SaveChangesAsync"/> call that stages the event
/// insert(s), so a read immediately after a write is never stale.
/// </summary>
internal sealed class MaterializedEntityStore<TEntity, TProjector>(DbContext context, EventStore eventStore)
    where TEntity : EntityBase, new()
    where TProjector : class, new()
{
    public Task<TEntity?> GetAsync(Guid entityId, CancellationToken cancellationToken = default) =>
        context.Set<TEntity>().FirstOrDefaultAsync(entity => entity.Id == entityId, cancellationToken);

    public async Task<TEntity> AppendAsync(
        Guid entityId,
        int expectedVersion,
        IReadOnlyList<object> payloads,
        CancellationToken cancellationToken = default
    )
    {
        var current = await GetAsync(entityId, cancellationToken);
        return await AppendCoreAsync(entityId, current, expectedVersion, payloads, cancellationToken);
    }

    /// <summary>
    /// Runs a command through the entity's <see cref="IDecide{TEntity,TCommand}"/> against its
    /// current materialized state (not a replay - see design.md) and appends the resulting events.
    /// </summary>
    public async Task<TEntity> HandleAsync<TCommand, TDecide>(
        Guid entityId,
        TCommand command,
        CancellationToken cancellationToken = default
    )
        where TCommand : class
        where TDecide : IDecide<TEntity, TCommand>, new()
    {
        var current = await GetAsync(entityId, cancellationToken);
        var state = current ?? new TEntity { Id = entityId };

        var payloads = new TDecide().Decide(state, command);

        return await AppendCoreAsync(entityId, current, state.Version, payloads, cancellationToken);
    }

    private async Task<TEntity> AppendCoreAsync(
        Guid entityId,
        TEntity? current,
        int expectedVersion,
        IReadOnlyList<object> payloads,
        CancellationToken cancellationToken
    )
    {
        var events = await eventStore.StageAsync(entityId, expectedVersion, payloads, cancellationToken);

        var seed = current ?? new TEntity { Id = entityId };
        var updated = Projector<TEntity, TProjector>.Fold(seed, events);

        if (current is null)
        {
            context.Set<TEntity>().Add(updated);
        }
        else
        {
            // `current` may already be tracked (e.g. read earlier in this same context by
            // HandleAsync); Update() would attach `updated` as a conflicting second instance with
            // the same key, so copy values into the existing tracked entry instead.
            context.Entry(current).CurrentValues.SetValues(updated);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new EventStoreConcurrencyException(entityId, expectedVersion, exception);
        }

        return updated;
    }
}
