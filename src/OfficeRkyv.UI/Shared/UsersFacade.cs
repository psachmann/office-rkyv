using OfficeRkyv.Contracts.Users;

namespace OfficeRkyv.UI.Shared;

/// <summary>
/// Queries are shared: one per circuit, so every component showing the same key shares one cache entry.
/// Mutations are not: a <c>Create…Mutation</c> call returns a fresh instance that the calling component owns
/// and disposes, so its state only ever reports executions that component started.
/// </summary>
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

    public IMutation<CreateUserRequest, UserDto> CreateCreateUserMutation() =>
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
    }
}
