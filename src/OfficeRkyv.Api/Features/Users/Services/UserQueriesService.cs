using Grpc.Core;

namespace OfficeRkyv.Api.Features.Users.Services;

public class UserQueriesService(IMediator mediator) : UserQueries.UserQueriesBase
{
    public override async Task<FindAllUsersResult> FindAllUsers(FindAllUsersQuery request, ServerCallContext context)
    {
        var result = await mediator.Send(request, context.CancellationToken);

        return result;
    }

    public override async Task<FindUserByEmailResult> FindUserByEmail(
        FindUserByEmailQuery request,
        ServerCallContext context
    )
    {
        var result = await mediator.Send(request, context.CancellationToken);

        return result;
    }
}
