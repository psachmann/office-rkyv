using Fisher;
using JasperFx.Events.Documents;

namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class FindAllUsersHandler
{
    public static Task<IReadOnlyList<User>> Handle(FindAllUsersQuery query, IQuerySession session) =>
        session.Query<User>().Where(user => !user.IsDeleted).ToListAsync();
}

public static class FindUserByIdHandler
{
    public static Task<User?> Handle(FindUserByIdQuery query, IQuerySession session) =>
        session.LoadAsync<User>(query.UserId)!;
}

public static class FindUserByEmailHandler
{
    public static Task<User?> Handle(FindUserByEmailQuery query, IQuerySession session) =>
        session.Query<User>().Where(user => user.Email == query.Email && !user.IsDeleted).FirstOrDefaultAsync()!;
}
