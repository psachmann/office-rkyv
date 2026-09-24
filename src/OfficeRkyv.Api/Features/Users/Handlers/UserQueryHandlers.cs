namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class FindAllUsersHandler
{
    public static Task<IReadOnlyList<User>> Handle(FindAllUsersQuery query, IQuerySession session) =>
        session.Query<User>().Where(user => !user.IsDeleted).ToListAsync();
}
