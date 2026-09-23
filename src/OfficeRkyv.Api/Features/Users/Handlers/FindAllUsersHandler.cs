using Fisher;
using JasperFx.Events.Documents;

namespace OfficeRkyv.Api.Features.Users.Handlers;

public sealed class FindAllUsersHandler(IQuerySession session)
    : IQueryHandler<FindAllUsersQuery, IReadOnlyList<UserEntity>>
{
    public async ValueTask<IReadOnlyList<UserEntity>> Handle(
        FindAllUsersQuery query,
        CancellationToken cancellationToken
    ) => await session.Query<UserEntity>().Where(user => !user.IsDeleted).ToListAsync(cancellationToken);
}
