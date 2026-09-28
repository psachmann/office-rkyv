namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class FindUserByEmailHandler
{
    public static Task<User?> Handle(FindUserByEmailQuery query, IQuerySession session)
    {
        // Same notion of sameness as the uniqueness key, so a lookup in any case finds the user.
        var normalized = EmailAddress.Normalize(query.Email);

        return session
            .Query<User>()
            .Where(user => user.NormalizedEmail == normalized && user.Status != EntityStatus.Deleted)
            .FirstOrDefaultAsync()!;
    }
}
