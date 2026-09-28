using FluentValidation;
using FluentValidation.Results;
using Google.Rpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using OfficeRkyv.Api.Shared.Errors;
using OfficeRkyv.Api.Shared.Exceptions;
using OfficeRkyv.Contracts;
using GrpcStatus = Grpc.Core.Status;
using RpcStatus = Google.Rpc.Status;

namespace OfficeRkyv.UnitTests.Shared.Errors;

public sealed class ErrorStatusProviderTests
{
    private readonly CapturingLogger logger = new();

    private RpcStatus? Map(Exception failure, CancellationToken cancellation = default) =>
        new ErrorStatusProvider(logger).BuildStatus(failure, new FakeServerCallContext(cancellation));

    [Test]
    public async Task Validation_IsLeftToTheValidationProvider()
    {
        await Assert.That(Map(new ValidationException([new ValidationFailure("Email", "bad")]))).IsNull();
    }

    [Test]
    public async Task InvariantViolation_IsFailedPrecondition_WithItsCodeAsErrorInfoReason()
    {
        var status = Map(new InvariantViolationException(InvariantCodes.UserDeleted))!;

        await Assert.That(status.Code).IsEqualTo((int)Code.FailedPrecondition);

        var info = status.Details.Single().Unpack<ErrorInfo>();
        await Assert.That(info.Reason).IsEqualTo(InvariantCodes.UserDeleted);
        await Assert.That(info.Domain).IsEqualTo(ErrorDomain.Value);
        await Assert.That(status.Message).DoesNotContain(InvariantCodes.UserDeleted);
    }

    [Test]
    public async Task NotFound_IsNotFound_WithoutTheExceptionsText()
    {
        var status = Map(new NotFoundException("User 'x' does not exist."))!;

        await Assert.That(status.Code).IsEqualTo((int)Code.NotFound);
        await Assert.That(status.Message).DoesNotContain("'x'");
    }

    [Test]
    public async Task AnExistingRpcException_KeepsItsStatus()
    {
        var status = Map(new RpcException(new GrpcStatus(StatusCode.Unavailable, "backend down")))!;

        await Assert.That(status.Code).IsEqualTo((int)Code.Unavailable);
        await Assert.That(status.Message).IsEqualTo("backend down");
    }

    [Test]
    public async Task UnexpectedFailures_AreInternal_WithNoLeakedText_AndAreLogged()
    {
        var failure = new InvalidOperationException("SQLITE_BUSY at /var/data/secret.db");

        var status = Map(failure)!;

        await Assert.That(status.Code).IsEqualTo((int)Code.Internal);
        await Assert.That(status.Message).DoesNotContain("SQLITE");
        await Assert.That(status.Message).DoesNotContain("secret");
        await Assert.That(status.Details).IsEmpty();
        await Assert.That(logger.Entries.Single()).IsEqualTo((LogLevel.Error, (Exception?)failure));
    }

    [Test]
    public async Task ACancelledCall_IsCancelled_AndNotLogged()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var status = Map(new TaskCanceledException(), source.Token)!;

        await Assert.That(status.Code).IsEqualTo((int)Code.Cancelled);
        await Assert.That(logger.Entries).IsEmpty();
    }

    [Test]
    public async Task ACancellationTheClientDidNotAskFor_IsAnUnexpectedFailure()
    {
        var status = Map(new TaskCanceledException())!;

        await Assert.That(status.Code).IsEqualTo((int)Code.Internal);
        await Assert.That(logger.Entries).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ValidationFailures_BecomeFieldViolations_WithTheCodeAsReason()
    {
        var failure = new ValidationException([
            new ValidationFailure("Email", "'Email' is not valid.") { ErrorCode = ViolationCodes.InvalidFormat },
            new ValidationFailure("DisplayName", "'Display Name' is empty.") { ErrorCode = ViolationCodes.Required },
        ]);

        var adapter = new ValidationFailureAdapter();

        await Assert.That(adapter.CanHandle(failure)).IsTrue();
        await Assert
            .That(adapter.ToFieldViolations(failure).Select(v => (v.Field, v.Reason)))
            .IsEquivalentTo([("email", ViolationCodes.InvalidFormat), ("display_name", ViolationCodes.Required)]);
    }

    [Test]
    [Arguments("Email", "email")]
    [Arguments("DisplayName", "display_name")]
    [Arguments("UserId", "user_id")]
    public async Task ToSnakeCase_MatchesTheProtoFieldNames(string property, string expected)
    {
        await Assert.That(ValidationFailureAdapter.ToSnakeCase(property)).IsEqualTo(expected);
    }

    private sealed class CapturingLogger : ILogger<ErrorStatusProvider>
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
        protected override string MethodCore => "/users.UsersService/CreateUser";
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
