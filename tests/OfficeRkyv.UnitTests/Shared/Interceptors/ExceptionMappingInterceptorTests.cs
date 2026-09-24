using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using OfficeRkyv.Api.Shared.Exceptions;
using OfficeRkyv.Api.Shared.Interceptors;
using GrpcStatus = Grpc.Core.Status;

namespace OfficeRkyv.UnitTests.Shared.Interceptors;

public sealed class ExceptionMappingInterceptorTests
{
    private readonly CapturingLogger logger = new();

    private async Task<RpcException> Invoke(Exception failure, CancellationToken cancellation = default)
    {
        var interceptor = new ExceptionMappingInterceptor(logger);

        try
        {
            await interceptor.UnaryServerHandler<string, string>(
                "request",
                new FakeServerCallContext(cancellation),
                (_, _) => throw failure
            );
        }
        catch (RpcException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected the call to fail with an RpcException.");
    }

    [Test]
    public async Task ASuccessfulCall_PassesThroughUntouched()
    {
        var interceptor = new ExceptionMappingInterceptor(logger);

        var response = await interceptor.UnaryServerHandler<string, string>(
            "request",
            new FakeServerCallContext(),
            (request, _) => Task.FromResult($"echo {request}")
        );

        await Assert.That(response).IsEqualTo("echo request");
    }

    [Test]
    public async Task Validation_IsInvalidArgument_WithSnakeCasedFieldViolations()
    {
        var failure = new ValidationException([
            new FieldViolation("Email", ViolationCodes.InvalidFormat),
            new FieldViolation("DisplayName", ViolationCodes.Required),
        ]);

        var mapped = await Invoke(failure);

        await Assert.That(mapped.StatusCode).IsEqualTo(StatusCode.InvalidArgument);

        var details = mapped.GetRpcStatus()!.Details.Single().Unpack<BadRequest>();
        await Assert
            .That(details.FieldViolations.Select(v => (v.Field, v.Description)))
            .IsEquivalentTo([("email", "invalid_format"), ("display_name", "required")]);
    }

    [Test]
    [Arguments("Email", "email")]
    [Arguments("DisplayName", "display_name")]
    [Arguments("UserId", "user_id")]
    public async Task ToSnakeCase_MatchesTheProtoFieldNames(string property, string expected)
    {
        await Assert.That(ExceptionMappingInterceptor.ToSnakeCase(property)).IsEqualTo(expected);
    }

    [Test]
    public async Task InvariantViolation_IsFailedPrecondition_KeepingItsAppAuthoredMessage()
    {
        var mapped = await Invoke(new InvariantViolationException("A deleted user cannot be renamed."));

        await Assert.That(mapped.StatusCode).IsEqualTo(StatusCode.FailedPrecondition);
        await Assert.That(mapped.Status.Detail).IsEqualTo("A deleted user cannot be renamed.");
    }

    [Test]
    public async Task NotFound_IsNotFound()
    {
        var mapped = await Invoke(new NotFoundException("User 'x' does not exist."));

        await Assert.That(mapped.StatusCode).IsEqualTo(StatusCode.NotFound);
    }

    [Test]
    public async Task AnExistingRpcException_PassesThroughUnchanged()
    {
        var original = new RpcException(new GrpcStatus(StatusCode.Unavailable, "backend down"));

        var mapped = await Invoke(original);

        await Assert.That(mapped).IsSameReferenceAs(original);
    }

    [Test]
    public async Task UnexpectedFailures_AreInternal_WithNoLeakedText_AndAreLogged()
    {
        var secret = "SQLITE_BUSY at /var/data/secret.db";
        var failure = new InvalidOperationException(secret);

        var mapped = await Invoke(failure);

        await Assert.That(mapped.StatusCode).IsEqualTo(StatusCode.Internal);
        await Assert.That(mapped.Status.Detail).DoesNotContain("SQLITE");
        await Assert.That(mapped.Status.Detail).DoesNotContain("secret");
        await Assert.That(mapped.Message).DoesNotContain("secret");
        await Assert.That(logger.Entries.Single()).IsEqualTo((LogLevel.Error, (Exception?)failure));
    }

    [Test]
    public async Task ACancelledCall_IsNotMappedOrLogged()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var cancellation = new OperationCanceledException(source.Token);

        var interceptor = new ExceptionMappingInterceptor(logger);

        await Assert
            .That(async () =>
                await interceptor.UnaryServerHandler<string, string>(
                    "request",
                    new FakeServerCallContext(source.Token),
                    (_, _) => throw cancellation
                )
            )
            .Throws<OperationCanceledException>();

        await Assert.That(logger.Entries).IsEmpty();
    }

    private sealed class CapturingLogger : ILogger<ExceptionMappingInterceptor>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add((logLevel, exception));
    }

    private sealed class FakeServerCallContext(CancellationToken cancellation = default) : ServerCallContext
    {
        protected override string MethodCore => "/users.UserService/CreateUser";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "peer";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => [];
        protected override CancellationToken CancellationTokenCore => cancellation;
        protected override Metadata ResponseTrailersCore => [];
        protected override GrpcStatus StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(null, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }
}
