namespace OfficeRkyv.Api.Features.Users;

public record UserEntity : EntityBase
{
    public required string Email { get; init; }

    public required string DisplayName { get; init; }
}
