namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class DeleteUserHandler
{
    [DeciderFunction]
    public static IEnumerable<object> Handle(DeleteUserCommand command, User user)
    {
        if (user.Status is EntityStatus.Active)
        {
            yield return new UserDeleted();
        }
    }
}
