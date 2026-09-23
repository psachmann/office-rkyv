namespace OfficeRkyv.Api.Features.Users.Handlers;

public sealed class FindUserByEmailHandler : IQueryHandler<FindUserByEmailQuery, UserEntity?>
{
    public ValueTask<UserEntity?> Handle(FindUserByEmailQuery query, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
