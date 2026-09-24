namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class FindUserByIdHandler
{
    public static Task<User?> Handle(FindUserByIdQuery query, IQuerySession session) =>
        session.LoadAsync<User>(query.UserId)!;
}
