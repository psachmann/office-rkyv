namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class UpdateUserHandler
{
    public static Task ValidateAsync(UpdateUserCommand command, IQuerySession session) =>
        UserExistence.RequireAsync(session, command.UserId);

    /// <summary>
    /// decide(command, state) -> events. Wolverine loads the aggregate, enforces the expected
    /// version, appends whatever comes back and commits - none of which appears here.
    /// </summary>
    [DeciderFunction]
    public static IEnumerable<object> Handle(UpdateUserCommand command, User user)
    {
        if (user.Status is EntityStatus.Deleted)
        {
            throw new InvariantViolationException(InvariantCodes.UserDeleted);
        }

        if (user.DisplayName != command.DisplayName)
        {
            yield return new UserRenamed(command.DisplayName);
        }
    }
}
