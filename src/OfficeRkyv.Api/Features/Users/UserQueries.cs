namespace OfficeRkyv.Api.Features.Users;

public sealed record FindAllUsersQuery : IQuery<IReadOnlyList<UserEntity>>;

public sealed record FindUserByEmailQuery(string Email) : IQuery<UserEntity?>;
