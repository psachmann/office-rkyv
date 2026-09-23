using Grpc.Core;

namespace OfficeRkyv.Api.Features.Users.Services;

public class UserQueriesService(IMediator mediator) : UserQueries.UserQueriesBase
{
    public override async Task<FindAllUsersResponse> FindAllUsers(
        FindAllUsersRequest request,
        ServerCallContext context
    )
    {
        var query = request.ToQuery();
        var entities = await mediator.Send(query, context.CancellationToken);

        return entities.ToResponse();
    }

    public override async Task<FindUserByEmailResponse> FindUserByEmail(
        FindUserByEmailRequest request,
        ServerCallContext context
    )
    {
        var query = request.ToQuery();
        var user = await mediator.Send(query, context.CancellationToken);

        return user.ToResponse();
    }
}
