using FluentValidation;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using Wolverine.Grpc;
using RpcStatus = Google.Rpc.Status;

namespace OfficeRkyv.Api.Shared.Errors;

/// <summary>
/// Maps every non-validation failure to a gRPC status, and is registered last so it also catches whatever
/// nothing else claimed. Every message here is our own fixed text: an exception's own message can carry
/// SQL, paths or other internals, so it never reaches a client. Clients explain refusals from the status
/// code and <see cref="ErrorInfo.Reason"/>, never from these messages.
/// </summary>
internal sealed partial class ErrorStatusProvider(ILogger<ErrorStatusProvider> logger) : IGrpcStatusDetailsProvider
{
    public RpcStatus? BuildStatus(Exception exception, ServerCallContext context) =>
        exception switch
        {
            // Claimed by Wolverine's validation provider through ValidationFailureAdapter.
            ValidationException => null,

            // Already a deliberate status: pass it through untouched.
            RpcException rpc => rpc.GetRpcStatus() ?? Plain(rpc.StatusCode, rpc.Status.Detail),

            // The client went away; that is not a server fault and must not be logged or reported as one.
            OperationCanceledException when context.CancellationToken.IsCancellationRequested => Plain(
                StatusCode.Cancelled,
                "The call was cancelled."
            ),

            // The JasperFx base type, so Fisher's subclass and any other store's exception both match.
            DuplicateNaturalKeyException => Plain(StatusCode.AlreadyExists, "The resource already exists."),

            InvariantViolationException invariant => new RpcStatus
            {
                Code = (int)Code.FailedPrecondition,
                Message = "The resource's current state does not allow this operation.",
                Details = { Any.Pack(new ErrorInfo { Reason = invariant.Code, Domain = ErrorDomain.Value }) },
            },

            EventStreamUnexpectedMaxEventIdException => Plain(
                StatusCode.Aborted,
                "The resource was changed by someone else; reload it and try again."
            ),

            NotFoundException => Plain(StatusCode.NotFound, "The resource was not found."),

            // The same text for a missing, invalid or expired token and for a user that is gone: the reason must
            // not tell a caller which of them it was.
            UnauthenticatedException => Plain(StatusCode.Unauthenticated, "The call needs a signed-in user."),

            PermissionDeniedException => Plain(StatusCode.PermissionDenied, "The signed-in user may not do this."),

            _ => Unexpected(exception, context),
        };

    private RpcStatus Unexpected(Exception exception, ServerCallContext context)
    {
        LogUnhandledException(logger, exception, context.Method);

        return Plain(StatusCode.Internal, "An unexpected error occurred.");
    }

    private static RpcStatus Plain(StatusCode code, string message) => new() { Code = (int)code, Message = message };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception in gRPC call {Method}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method);
}
