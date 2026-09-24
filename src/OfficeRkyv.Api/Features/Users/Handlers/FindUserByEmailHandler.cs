namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class FindUserByEmailHandler
{
    public static Task<User?> Handle(FindUserByEmailQuery query, IQuerySession session) =>
        session.Query<User>().Where(user => user.Email == query.Email && !user.IsDeleted).FirstOrDefaultAsync()!;
}
