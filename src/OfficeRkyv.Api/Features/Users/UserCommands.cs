namespace OfficeRkyv.Api.Features.Users;

// No marker interfaces: Wolverine discovers handlers by their method signature, not by the message
// implementing ICommand<T>. UserId matches the {AggregateName}Id convention the decider workflow
// uses to find the stream; Version is the expected version, echoed back by the client.
public sealed record CreateUserCommand(string Email, string DisplayName);

public sealed record UpdateUserCommand(Guid UserId, int Version, string DisplayName);

public sealed record DeleteUserCommand(Guid UserId, int Version);
