namespace OfficeRkyv.Api.Features.Users.Handlers;

/// <summary>
/// Input rules live in <see cref="CreateUserValidator"/>; Wolverine's FluentValidation middleware
/// runs it before <see cref="Handle"/> and throws with every violated rule, so this only sees valid input.
/// </summary>
public static class CreateUserHandler
{
    /// <summary>
    /// A create has no prior state to decide against, so this starts the stream directly rather than
    /// going through the decider workflow. Uniqueness is the store's job: [NaturalKey] on
    /// <see cref="User.NormalizedEmail"/> claims the key inside the append transaction, so there is no
    /// check-then-write race to lose.
    /// </summary>
    public static async Task<User> Handle(CreateUserCommand command, IDocumentSession session)
    {
        var email = command.Email.Trim();
        var stream = session.Events.StartStream<User>(
            new UserCreated(email, EmailAddress.Normalize(email), command.DisplayName.Trim())
        );
        await session.SaveChangesAsync();

        // Inline projection: the snapshot was written in that same transaction, so this load is a
        // read of committed state rather than a race with a background projector.
        return await session.LoadAsync<User>(stream.Id)
            ?? throw new InvalidOperationException($"User stream '{stream.Id}' produced no snapshot.");
    }
}
