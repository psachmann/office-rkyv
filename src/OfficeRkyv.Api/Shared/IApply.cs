namespace OfficeRkyv.Api.Shared;

public interface IApply<TEntity, in TPayload>
    where TEntity : class, IEntity
    where TPayload : class
{
    public TEntity Apply(TEntity entity, TPayload payload);
}
