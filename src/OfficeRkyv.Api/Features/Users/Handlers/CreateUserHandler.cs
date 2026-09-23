namespace OfficeRkyv.Api.Features.Users.Handlers;

public sealed class CreateUserHandler : ICommandHandler<CreateUserCommand, Unit>
{
    public ValueTask<Unit> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
