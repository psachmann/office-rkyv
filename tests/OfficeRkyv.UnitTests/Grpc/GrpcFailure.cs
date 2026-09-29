using Google.Rpc;
using Grpc.Core;

namespace OfficeRkyv.UnitTests.Grpc;

/// <summary>What a gRPC client sees when a call fails: the status, and the structured detail behind it.</summary>
internal static class GrpcFailure
{
    public static async Task<RpcException> Of(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (RpcException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected the call to fail.");
    }

    public static T Detail<T>(RpcException failure)
        where T : Google.Protobuf.IMessage<T>, new() => failure.GetRpcStatus()!.Details.Single().Unpack<T>();

    public static string Reason(RpcException failure) => Detail<ErrorInfo>(failure).Reason;
}
