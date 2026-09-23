using Fisher;

namespace OfficeRkyv.Api.Features.Users.Handlers;

public sealed class DeleteUserHandler(IDocumentSession session) : ICommandHandler<DeleteUserCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        var stream = await session.Events.FetchForWritingByNaturalKey<UserEntity, string>(
            command.Email,
            cancellationToken
        );

        if (stream.Aggregate is { IsDeleted: false })
        {
            stream.AppendOne(new UserDeleted());
        }

        await session.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
