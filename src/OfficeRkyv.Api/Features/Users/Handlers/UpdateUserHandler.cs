using Fisher;

namespace OfficeRkyv.Api.Features.Users.Handlers;

public sealed class UpdateUserHandler(IDocumentSession session) : ICommandHandler<UpdateUserCommand, Unit>
{
    public async ValueTask<Unit> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        // Read-decide-append against the stream the natural key resolves to. The optimistic
        // concurrency check runs inside the write transaction.
        var stream = await session.Events.FetchForWritingByNaturalKey<UserEntity, string>(
            command.Email,
            cancellationToken
        );

        if (stream.Aggregate?.DisplayName != command.DisplayName)
        {
            stream.AppendOne(new UserRenamed(command.DisplayName));
        }

        await session.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
