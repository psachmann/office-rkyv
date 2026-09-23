namespace OfficeRkyv.Api.Features.Users.Handlers;

public sealed class FindUserByEmailHandler : IQueryHandler<FindAllUsersQuery, FindAllUsersResult>
{
    public ValueTask<FindAllUsersResult> Handle(FindAllUsersQuery query, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
