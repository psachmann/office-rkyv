namespace OfficeRkyv.Api.Features.Users;

public sealed record FindAllUsersQuery;

public sealed record FindUserByIdQuery(Guid UserId);

public sealed record FindUserByEmailQuery(string Email);
