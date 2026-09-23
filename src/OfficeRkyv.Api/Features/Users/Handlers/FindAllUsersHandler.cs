namespace OfficeRkyv.Api.Features.Users.Handlers;

public sealed class FindAllUsersHandler : IQueryHandler<FindAllUsersQuery, IReadOnlyList<UserEntity>>
{
    public ValueTask<IReadOnlyList<UserEntity>> Handle(FindAllUsersQuery query, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
