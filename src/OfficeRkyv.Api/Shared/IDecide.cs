namespace OfficeRkyv.Api.Shared;

public interface IDecide<TEntity, in TCommand>
    where TEntity : class, IEntity
    where TCommand : class
{
    public IReadOnlyList<object> Decide(TEntity state, TCommand command);
}
