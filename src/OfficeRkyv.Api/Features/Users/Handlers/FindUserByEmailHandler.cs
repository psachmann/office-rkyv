using Fisher;
using JasperFx.Events.Documents;

namespace OfficeRkyv.Api.Features.Users.Handlers;

public sealed class FindUserByEmailHandler(IQuerySession session) : IQueryHandler<FindUserByEmailQuery, UserEntity?>
{
    public async ValueTask<UserEntity?> Handle(FindUserByEmailQuery query, CancellationToken cancellationToken) =>
        await session
            .Query<UserEntity>()
            .Where(user => user.Email == query.Email && !user.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
}
