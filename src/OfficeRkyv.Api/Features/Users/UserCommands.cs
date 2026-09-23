namespace OfficeRkyv.Api.Features.Users;

public sealed record CreateUserCommand(string Email, string DisplayName) : ICommand<Unit>;

public sealed record UpdateUserCommand(string Email, string DisplayName) : ICommand<Unit>;

public sealed record DeleteUserCommand(string Email) : ICommand<Unit>;
