namespace OfficeRkyv.Api.Features.Users.Handlers;

public static class CreateUserHandler
{
    public const int MaxDisplayNameLength = 100;

    /// <summary>
    /// Runs before <see cref="Handle"/> (Wolverine's naming convention) and reports every violated
    /// rule at once. Returns void and throws on purpose: Wolverine gives Validate methods that return a
    /// RequirementResult or string[] a different meaning - log and silently stop the handler - and a
    /// caller waiting on the created user must instead see the failure.
    /// </summary>
    public static void Validate(CreateUserCommand command)
    {
        var violations = new List<FieldViolation>();

        var email = command.Email.Trim();

        if (email.Length == 0)
        {
            violations.Add(new(nameof(CreateUserCommand.Email), ViolationCodes.Required));
        }
        else
        {
            if (email.Length > EmailAddress.MaxLength)
            {
                violations.Add(new(nameof(CreateUserCommand.Email), ViolationCodes.TooLong));
            }

            if (!EmailAddress.IsWellFormed(email))
            {
                violations.Add(new(nameof(CreateUserCommand.Email), ViolationCodes.InvalidFormat));
            }
        }

        var displayName = command.DisplayName.Trim();

        if (displayName.Length == 0)
        {
            violations.Add(new(nameof(CreateUserCommand.DisplayName), ViolationCodes.Required));
        }
        else if (displayName.Length > MaxDisplayNameLength)
        {
            violations.Add(new(nameof(CreateUserCommand.DisplayName), ViolationCodes.TooLong));
        }

        if (violations.Count > 0)
        {
            throw new ValidationException(violations);
        }
    }

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
