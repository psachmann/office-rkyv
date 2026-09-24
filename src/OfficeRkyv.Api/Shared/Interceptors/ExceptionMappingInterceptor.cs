using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using Grpc.Core.Interceptors;
using GrpcStatus = Grpc.Core.Status;

namespace OfficeRkyv.Api.Shared.Interceptors;

/// <summary>
/// The one place domain and store exceptions become gRPC statuses, for every service the API hosts.
/// Handlers keep throwing what they throw and know nothing about gRPC.
/// </summary>
public sealed class ExceptionMappingInterceptor(ILogger<ExceptionMappingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation
    )
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // The client went away; that is not a server fault and must not be logged or reported as one.
            throw;
        }
        catch (Exception exception)
        {
            var mapped = Map(exception);

            if (mapped.StatusCode is StatusCode.Internal)
            {
                logger.LogError(exception, "Unhandled exception in gRPC call {Method}", context.Method);
            }

            throw mapped;
        }
    }

    internal static RpcException Map(Exception exception) =>
        exception switch
        {
            ValidationException validation => InvalidArgument(validation),

            // The JasperFx base type, so Fisher's subclass and any other store's exception both match.
            DuplicateNaturalKeyException => Failure(StatusCode.AlreadyExists, "The resource already exists."),

            // App-authored and safe to show, unlike everything below.
            InvariantViolationException invariant => Failure(StatusCode.FailedPrecondition, invariant.Message),

            EventStreamUnexpectedMaxEventIdException => Failure(
                StatusCode.Aborted,
                "The resource was changed by someone else; reload it and try again."
            ),

            NotFoundException => Failure(StatusCode.NotFound, "The resource was not found."),

            // Never the exception's own text: it can carry SQL, paths or other internals.
            _ => Failure(StatusCode.Internal, "An unexpected error occurred."),
        };

    private static RpcException Failure(StatusCode code, string message) => new(new GrpcStatus(code, message));

    private static RpcException InvalidArgument(ValidationException validation)
    {
        var details = new BadRequest();

        foreach (var violation in validation.Violations)
        {
            details.FieldViolations.Add(
                new BadRequest.Types.FieldViolation
                {
                    // The domain reports its own property names; the contract's field names are snake_case.
                    Field = ToSnakeCase(violation.Field),
                    Description = violation.Code,
                }
            );
        }

        return new Google.Rpc.Status
        {
            Code = (int)Code.InvalidArgument,
            Message = "The request is invalid.",
            Details = { Any.Pack(details) },
        }.ToRpcException();
    }

    internal static string ToSnakeCase(string name) =>
        string.Concat(
            name.Select(
                (c, i) => i > 0 && char.IsUpper(c) ? $"_{char.ToLowerInvariant(c)}" : $"{char.ToLowerInvariant(c)}"
            )
        );
}
