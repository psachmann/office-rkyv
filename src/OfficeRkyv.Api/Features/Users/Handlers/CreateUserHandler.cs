using Fisher;

namespace OfficeRkyv.Api.Features.Users.Handlers;

public sealed class CreateUserHandler(IDocumentSession session) : ICommandHandler<CreateUserCommand, Unit>
{
    public async ValueTask<Unit> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        session.Events.StartStream<UserEntity>(new UserCreated(command.Email, command.DisplayName));
        await session.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
