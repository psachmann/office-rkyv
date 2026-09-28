namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class DeleteUserHandler
{
    public static Task ValidateAsync(DeleteUserCommand command, IQuerySession session) =>
        UserExistence.RequireAsync(session, command.UserId);

    [DeciderFunction]
    public static IEnumerable<object> Handle(DeleteUserCommand command, User user)
    {
        if (user.Status is EntityStatus.Active)
        {
            yield return new UserDeleted();
        }
    }
}
