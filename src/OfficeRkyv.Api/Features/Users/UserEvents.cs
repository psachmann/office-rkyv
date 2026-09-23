namespace OfficeRkyv.Api.Features.Users;

public record UserCreatedEvent : EventBase<UserCreatedPayload>;

public record UserCreatedPayload
{
    public required string Email { get; init; }

    public required string DisplayName { get; init; }
}

public record UserUpdatedEvent : EventBase<UserUpdatedPayload>;

public record UserUpdatedPayload
{
    public required string Email { get; init; }

    public required string DisplayName { get; init; }
}

public record UserDeletedEvent : EventBase<UserDeletedPayload>;

public record UserDeletedPayload
{
    public required string DisplayName { get; init; }
}
