using Grpc.Core;
using Wolverine;

namespace OfficeRkyv.Api.Features.Users.Services;

public class UserQueriesService(IMessageBus bus) : UserQueries.UserQueriesBase
{
    public override async Task<FindAllUsersResponse> FindAllUsers(
        FindAllUsersRequest request,
        ServerCallContext context
    )
    {
        var users = await bus.InvokeAsync<IReadOnlyList<User>>(request.ToQuery(), context.CancellationToken);

        return users.ToResponse();
    }

    public override async Task<FindUserByEmailResponse> FindUserByEmail(
        FindUserByEmailRequest request,
        ServerCallContext context
    )
    {
        var user = await bus.InvokeAsync<User?>(request.ToQuery(), context.CancellationToken);

        return user.ToResponse();
    }
}
