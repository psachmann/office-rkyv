using Google.Protobuf.WellKnownTypes;
using Riok.Mapperly.Abstractions;

namespace OfficeRkyv.Api.Features.Users;

[Mapper]
public static partial class UserMapper
{
    public static partial UserDto ToUserDto(this UserEntity entity);

    private static Timestamp ToTimestamp(DateTimeOffset value) => Timestamp.FromDateTimeOffset(value);
}
