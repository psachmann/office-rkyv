using System.Collections.Concurrent;
using System.Reflection;

namespace OfficeRkyv.Api.Data;

/// <summary>
/// Folds a heterogeneous event stream into <typeparamref name="TEntity"/> state by dispatching each
/// event's payload to the matching <see cref="IApply{TEntity,TPayload}"/> implementation on
/// <typeparamref name="TProjector"/>, cached per payload type the same way <see cref="EventTypeName"/>
/// caches attribute lookups. Soft delete (<see cref="Deleted"/>) is handled here directly, not by the
/// projector, since it is an infrastructure-owned transition every entity shares.
/// </summary>
internal static class Projector<TEntity, TProjector>
    where TEntity : EntityBase
    where TProjector : class, new()
{
    private static readonly TProjector Instance = new();
    private static readonly ConcurrentDictionary<Type, Func<TProjector, TEntity, object, TEntity>> Dispatch = new();

    public static TEntity Apply(TEntity entity, IEvent @event)
    {
        var applied =
            @event.Payload is Deleted
                ? entity.MarkDeleted(@event.Timestamp)
                : Dispatch.GetOrAdd(@event.Payload.GetType(), BuildDispatch)(Instance, entity, @event.Payload);

        return (TEntity)applied.WithVersion(@event.Version);
    }

    public static TEntity Fold(TEntity seed, IEnumerable<IEvent> events)
    {
        var state = seed;

        foreach (var @event in events)
        {
            state = Apply(state, @event);
        }

        return state;
    }

    private static Func<TProjector, TEntity, object, TEntity> BuildDispatch(Type payloadType)
    {
        var interfaceType = typeof(IApply<,>).MakeGenericType(typeof(TEntity), payloadType);

        if (!interfaceType.IsInstanceOfType(Instance))
        {
            throw new InvalidOperationException(
                $"'{typeof(TProjector).FullName}' does not implement IApply<{typeof(TEntity).Name}, {payloadType.Name}> for event payload '{payloadType.FullName}'."
            );
        }

        var method = interfaceType.GetMethod(nameof(IApply<TEntity, object>.Apply))!;

        return (projector, entity, payload) => (TEntity)method.Invoke(projector, [entity, payload])!;
    }
}
