using System.Security.Claims;
using Grpc.Core;
using OfficeRkyv.Api.Features.Users;
using Wolverine;

namespace OfficeRkyv.Api.Shared.Auth;

/// <summary>
/// Tells a service who is calling. It sits in the services and not in an authorization attribute, so that a
/// refusal takes the same path to a status as every other failure, with the API's own fixed text.
/// </summary>
/// <remarks>
/// The token only says which user it was issued for. The user is looked up on every call, because a token lives
/// for minutes and the user may have been deleted since.
/// </remarks>
public sealed class CallerAccessor(IMessageBus bus)
{
    /// <summary>The id of the user making the call.</summary>
    /// <exception cref="UnauthenticatedException">
    /// The call has no accepted token, the token names no user id, or that user does not exist or was deleted.
    /// </exception>
    public async Task<Guid> RequireAsync(ServerCallContext context)
    {
        var principal = context.GetHttpContext().User;

        if (
            principal.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirstValue("sub"), out var userId)
        )
        {
            throw new UnauthenticatedException("The call carries no accepted caller token.");
        }

        var user = await bus.InvokeAsync<User?>(new FindUserByIdQuery(userId), context.CancellationToken);

        if (user is not { Status: EntityStatus.Active })
        {
            throw new UnauthenticatedException($"The caller '{userId}' does not exist or was deleted.");
        }

        return userId;
    }
}
