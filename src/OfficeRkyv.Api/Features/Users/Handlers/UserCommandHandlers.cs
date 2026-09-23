using Fisher;
using Wolverine.Persistence.EventSourcing;

namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class CreateUserHandler
{
    /// <summary>
    /// A create has no prior state to decide against, so this starts the stream directly rather than
    /// going through the decider workflow. Uniqueness is the store's job: [NaturalKey] on
    /// <see cref="User.Email"/> claims the key inside the append transaction, so there is no
    /// check-then-write race to lose.
    /// </summary>
    public static async Task<User> Handle(CreateUserCommand command, IDocumentSession session)
    {
        var stream = session.Events.StartStream<User>(new UserCreated(command.Email, command.DisplayName));
        await session.SaveChangesAsync();

        // Inline projection: the snapshot was written in that same transaction, so this load is a
        // read of committed state rather than a race with a background projector.
        return await session.LoadAsync<User>(stream.Id)
            ?? throw new InvalidOperationException($"User stream '{stream.Id}' produced no snapshot.");
    }
}

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
