namespace OfficeRkyv.Api.Features.Users.Handlers;

public sealed class DeleteUserHandler : ICommandHandler<DeleteUserCommand, Unit>
{
    public ValueTask<Unit> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
