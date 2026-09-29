using OfficeRkyv.Contracts.Users;

namespace OfficeRkyv.Web.Shared;

public sealed class UsersFacade(IQueryClient queryClient, UsersService.UsersServiceClient userService) : IDisposable
{
    private static readonly QueryKey UsersKey = ["users"];

    public IQuery<Unit, IReadOnlyList<UserDto>> FindAllUsers { get; } =
        queryClient.CreateQuery(
            new QueryOptions<Unit, IReadOnlyList<UserDto>>
            {
                KeyFactory = _ => UsersKey,
                Fetcher = async (_, ct) =>
                    [.. (await userService.ListUsersAsync(new ListUsersRequest(), null, null, ct)).Users],
            }
        );

    // A user is either found or not, and "not found" is a normal answer rather than an error, so the
    // response itself is the data: an unset User tells the two apart.
    public IQuery<Guid, FindUserByIdResponse> FindUserById { get; } =
        queryClient.CreateQuery(
            new QueryOptions<Guid, FindUserByIdResponse>
            {
                KeyFactory = userId => ["users", userId],
                Fetcher = async (userId, ct) =>
                    await userService.FindUserByIdAsync(
                        new FindUserByIdRequest { UserId = userId.ToString() },
                        null,
                        null,
                        ct
                    ),
            }
        );

    // Lives as long as the facade (one per circuit), so its Success/Failure streams replay the last
    // outcome to new subscribers - a component should only react to executions it started.
    public IMutation<CreateUserRequest, UserDto> CreateUser { get; } =
        queryClient.CreateMutation(
            new MutationOptions<CreateUserRequest, UserDto>
            {
                Mutator = async (request, ct) => (await userService.CreateUserAsync(request, null, null, ct)).User,
                InvalidateKeys = [UsersKey],
            }
        );

    public void Dispose()
    {
        FindAllUsers.Dispose();
        FindUserById.Dispose();
        CreateUser.Dispose();
    }
}
