using Google.Protobuf.WellKnownTypes;
using Riok.Mapperly.Abstractions;

namespace OfficeRkyv.Api.Features.Users;

[Mapper]
public static partial class UserMapper
{
    public static partial CreateUserCommand ToCommand(this CreateUserRequest request);

    public static UpdateUserCommand ToCommand(this UpdateUserRequest request) =>
        new(Guid.Parse(request.UserId), request.Version, request.DisplayName);

    public static DeleteUserCommand ToCommand(this DeleteUserRequest request) =>
        new(Guid.Parse(request.UserId), request.Version);

    public static FindAllUsersQuery ToQuery(this ListUsersRequest request) => new();

    public static partial FindUserByEmailQuery ToQuery(this FindUserByEmailRequest request);

    // NormalizedEmail is a storage concern (the uniqueness key); the contract exposes Email only.
    // Status is not exposed either: deleted users are filtered out before they reach the API.
    [MapperIgnoreSource(nameof(User.NormalizedEmail))]
    [MapperIgnoreSource(nameof(User.Status))]
    public static partial UserDto ToDto(this User user);

    // Hand-written: `repeated` fields are get-only RepeatedField<T>, so items must be added, not assigned.
    public static ListUsersResponse ToResponse(this IEnumerable<User> users) =>
        new() { Users = { users.Select(ToDto) } };

    public static FindUserByEmailResponse ToResponse(this User? user) => new() { User = user?.ToDto() };

    public static CreateUserResponse ToCreateResponse(this User user) => new() { User = user.ToDto() };

    public static UpdateUserResponse ToUpdateResponse(this User user) => new() { User = user.ToDto() };

    private static Timestamp ToTimestamp(DateTimeOffset value) => Timestamp.FromDateTimeOffset(value);
}
