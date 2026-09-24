using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Wolverine;

namespace OfficeRkyv.Api.Features.Users;

public class UserGrpcService(IMessageBus bus) : UserService.UserServiceBase
{
    public override async Task<ListUsersResponse> ListUsers(ListUsersRequest request, ServerCallContext context)
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

    public override async Task<CreateUserResponse> CreateUser(CreateUserRequest request, ServerCallContext context)
    {
        var user = await bus.InvokeAsync<User>(request.ToCommand(), context.CancellationToken);

        return user.ToCreateResponse();
    }

    public override async Task<UpdateUserResponse> UpdateUser(UpdateUserRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();
        await bus.InvokeAsync(command, context.CancellationToken);

        // Inline projection: the snapshot is already current, so this reads the result of the write
        // that just happened rather than racing a background projector.
        var user = await bus.InvokeAsync<User?>(new FindUserByIdQuery(command.UserId), context.CancellationToken);

        return user!.ToUpdateResponse();
    }

    public override async Task<Empty> DeleteUser(DeleteUserRequest request, ServerCallContext context)
    {
        await bus.InvokeAsync(request.ToCommand(), context.CancellationToken);

        return new();
    }
}
