using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Wolverine;

namespace OfficeRkyv.Api.Features.Users.Services;

public class UserCommandsService(IMessageBus bus) : UserCommands.UserCommandsBase
{
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
