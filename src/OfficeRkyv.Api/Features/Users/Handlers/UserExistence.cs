namespace OfficeRkyv.Api.Features.Users.Handlers;

internal static class UserExistence
{
    /// <summary>
    /// Runs from the handlers' Validate step, which Wolverine executes before the decider loads the
    /// stream and checks the expected version. Without it, an unknown id is indistinguishable from a
    /// stale version (both surface as an unexpected-version conflict) or, at version 0, as a null user.
    /// A soft-deleted user still exists, so this passes and the decider applies its own rules.
    /// </summary>
    public static async Task RequireAsync(IQuerySession session, Guid userId)
    {
        if (await session.LoadAsync<User>(userId) is null)
        {
            throw new NotFoundException($"User '{userId}' does not exist.");
        }
    }
}
