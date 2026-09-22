using System.Linq.Expressions;

namespace OfficeRkyv.Api.Shared;

public interface ISpecification<TEntity>
    where TEntity : class, IEntity
{
    public Expression<Func<TEntity, bool>> Predicate { get; }

    public bool IsSatisfiedBy(TEntity entity);
}
