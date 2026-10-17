using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OfficeRkyv.Api.Shared.Auth;
using Wolverine;

namespace OfficeRkyv.Api.Features.Users;

public class UsersServiceImpl(IMessageBus bus, CallerAccessor caller) : UsersService.UsersServiceBase
{
    public override async Task<ListUsersResponse> ListUsers(ListUsersRequest request, ServerCallContext context)
    {
        var users = await bus.InvokeAsync<IReadOnlyList<User>>(request.ToQuery(), context.CancellationToken);

        return users.ToResponse();
    }

    public override async Task<FindUserByIdResponse> FindUserById(
        FindUserByIdRequest request,
        ServerCallContext context
    )
    {
        var user = await bus.InvokeAsync<User?>(request.ToQuery(), context.CancellationToken);

        // The handler also returns deleted users (UpdateUser reads its result back through it), but the
        // API reports a deleted user as not found, like the list does.
        return (user is { Status: EntityStatus.Active } ? user : null).ToFindByIdResponse();
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
        await RequireSelfAsync(command.UserId, context);
        await bus.InvokeAsync(command, context.CancellationToken);

        // Inline projection: the snapshot is already current, so this reads the result of the write
        // that just happened rather than racing a background projector.
        var user = await bus.InvokeAsync<User?>(new FindUserByIdQuery(command.UserId), context.CancellationToken);

        return user!.ToUpdateResponse();
    }

    public override async Task<Empty> DeleteUser(DeleteUserRequest request, ServerCallContext context)
    {
        var command = request.ToCommand();
        await RequireSelfAsync(command.UserId, context);
        await bus.InvokeAsync(command, context.CancellationToken);

        return new();
    }

    // Listing, finding and creating users are open, because the landing page calls them signed out. Changing a
    // user is the user's own business, whether or not the one named exists.
    private async Task RequireSelfAsync(Guid userId, ServerCallContext context)
    {
        if (await caller.RequireAsync(context) != userId)
        {
            throw new PermissionDeniedException($"The caller may not change user '{userId}'.");
        }
    }
}
