using Wolverine.Grpc;

namespace OfficeRkyv.Api.Services;

[WolverineGrpcService]
public abstract class GreeterService : Greeter.GreeterBase;

public class GreeterHandler(ILogger<GreeterHandler> logger)
{
    public HelloReply Handle(HelloRequest request)
    {
        logger.LogInformation("The message is received from {Name}", request.Name);

        return new HelloReply { Message = "Hello " + request.Name };
    }
}
