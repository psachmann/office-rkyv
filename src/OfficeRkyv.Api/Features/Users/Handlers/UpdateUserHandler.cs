namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class UpdateUserHandler
{
    /// <summary>
    /// decide(command, state) -> events. Wolverine loads the aggregate, enforces the expected
    /// version, appends whatever comes back and commits - none of which appears here.
    /// </summary>
    [DeciderFunction]
    public static IEnumerable<object> Handle(UpdateUserCommand command, User user)
    {
        if (user.IsDeleted)
        {
            throw new InvariantViolationException("A deleted user cannot be renamed.");
        }

        if (user.DisplayName != command.DisplayName)
        {
            yield return new UserRenamed(command.DisplayName);
        }
    }
}
