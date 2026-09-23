using Google.Protobuf.WellKnownTypes;
using Riok.Mapperly.Abstractions;

namespace OfficeRkyv.Api.Features.Users;

[Mapper]
public static partial class UserMapper
{
    public static partial CreateUserCommand ToCommand(this CreateUserRequest request);

    public static partial UpdateUserCommand ToCommand(this UpdateUserRequest request);

    public static partial DeleteUserCommand ToCommand(this DeleteUserRequest request);

    public static FindAllUsersQuery ToQuery(this FindAllUsersRequest request) => new();

    public static partial FindUserByEmailQuery ToQuery(this FindUserByEmailRequest request);

    public static partial UserDto ToDto(this UserEntity entity);

    // Hand-written: `repeated` fields are get-only RepeatedField<T>, so items must be added, not assigned.
    public static FindAllUsersResponse ToResponse(this IEnumerable<UserEntity> entities) =>
        new() { Users = { entities.Select(ToDto) } };

    public static FindUserByEmailResponse ToResponse(this UserEntity? entity) => new() { User = entity?.ToDto() };

    private static Timestamp ToTimestamp(DateTimeOffset value) => Timestamp.FromDateTimeOffset(value);
}
