using Grpc.Core;

namespace OfficeRkyv.Api.Services;

public partial class GreeterService(ILogger<GreeterService> logger) : Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        LogMessageReceived(logger, request.Name);

        return Task.FromResult(new HelloReply { Message = "Hello " + request.Name });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The message is received from {Name}")]
    private static partial void LogMessageReceived(ILogger logger, string name);
}
