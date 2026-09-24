namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class DeleteUserHandler
{
    [DeciderFunction]
    public static IEnumerable<object> Handle(DeleteUserCommand command, User user)
    {
        if (!user.IsDeleted)
        {
            yield return new UserDeleted();
        }
    }
}
