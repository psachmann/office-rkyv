using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace OfficeRkyv.Api.Features.Users.Services;

public class UserCommandsService(IMediator mediator) : UserCommands.UserCommandsBase
{
    public override async Task<Empty> CreateUser(CreateUserRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();
        var _ = await mediator.Send(command, context.CancellationToken);

        return new();
    }

    public override async Task<Empty> UpdateUser(UpdateUserRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();
        var _ = await mediator.Send(command, context.CancellationToken);

        return new();
    }

    public override async Task<Empty> DeleteUser(DeleteUserRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();
        var _ = await mediator.Send(command, context.CancellationToken);

        return new();
    }
}
