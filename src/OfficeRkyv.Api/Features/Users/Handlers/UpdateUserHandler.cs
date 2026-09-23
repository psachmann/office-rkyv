namespace OfficeRkyv.Api.Features.Users.Handlers;

public sealed class UpdateUserHandler : ICommandHandler<UpdateUserCommand, Unit>
{
    public ValueTask<Unit> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
