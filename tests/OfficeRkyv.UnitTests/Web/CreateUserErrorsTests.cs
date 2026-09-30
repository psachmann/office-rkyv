using System.Globalization;
using System.Resources;
using Google.Rpc;
using Grpc.Core;
using OfficeRkyv.UI.Components.Pages.Users;
using GrpcStatus = Grpc.Core.Status;
using ProtoAny = Google.Protobuf.WellKnownTypes.Any;

namespace OfficeRkyv.UnitTests.Web;

public sealed class CreateUserErrorsTests
{
    private static RpcException InvalidArgument(params (string Field, string Code)[] violations)
    {
        var details = new BadRequest();

        foreach (var (field, code) in violations)
        {
            details.FieldViolations.Add(
                new BadRequest.Types.FieldViolation
                {
                    Field = field,
                    Reason = code,
                    Description = "Developer text the form must never show.",
                }
            );
        }

        return new Google.Rpc.Status
        {
            Code = (int)Code.InvalidArgument,
            Message = "The request is invalid.",
            Details = { ProtoAny.Pack(details) },
        }.ToRpcException();
    }

    [Test]
    public async Task AlreadyExists_IsTheEmailTakenMessage_UnderTheEmailField()
    {
        var errors = CreateUserErrors.From(new RpcException(new GrpcStatus(StatusCode.AlreadyExists, "server text")));

        await Assert.That(errors).IsEqualTo(new CreateUserErrors(Email: "EmailTaken"));
    }

    [Test]
    [Arguments("email", "required", "EmailRequired")]
    [Arguments("email", "invalid_format", "EmailInvalid")]
    [Arguments("email", "too_long", "EmailTooLong")]
    [Arguments("email", "something_new", "FieldInvalid")]
    public async Task InvalidArgument_MapsEachEmailViolationToItsOwnMessage(string field, string code, string expected)
    {
        var errors = CreateUserErrors.From(InvalidArgument((field, code)));

        await Assert.That(errors.Email).IsEqualTo(expected);
        await Assert.That(errors.DisplayName).IsNull();
        await Assert.That(errors.General).IsNull();
    }

    [Test]
    [Arguments("required", "DisplayNameRequired")]
    [Arguments("too_long", "DisplayNameTooLong")]
    [Arguments("something_new", "FieldInvalid")]
    public async Task InvalidArgument_MapsDisplayNameViolations(string code, string expected)
    {
        var errors = CreateUserErrors.From(InvalidArgument(("display_name", code)));

        await Assert.That(errors.DisplayName).IsEqualTo(expected);
    }

    [Test]
    public async Task InvalidArgument_ReportsEveryFieldAtOnce_AndKeepsTheFirstViolationPerField()
    {
        var errors = CreateUserErrors.From(
            InvalidArgument(("email", "too_long"), ("email", "invalid_format"), ("display_name", "required"))
        );

        await Assert
            .That(errors)
            .IsEqualTo(new CreateUserErrors(Email: "EmailTooLong", DisplayName: "DisplayNameRequired"));
    }

    [Test]
    public async Task InvalidArgument_AboutAFieldTheFormDoesNotHave_BecomesTheGeneralMessage()
    {
        var errors = CreateUserErrors.From(InvalidArgument(("nickname", "required")));

        await Assert.That(errors).IsEqualTo(new CreateUserErrors(General: "CreateFailed"));
    }

    [Test]
    public async Task InvalidArgument_WithoutDetails_BecomesTheGeneralMessage()
    {
        var errors = CreateUserErrors.From(new RpcException(new GrpcStatus(StatusCode.InvalidArgument, "bad")));

        await Assert.That(errors).IsEqualTo(new CreateUserErrors(General: "CreateFailed"));
    }

    [Test]
    [Arguments(StatusCode.Unavailable)]
    [Arguments(StatusCode.Internal)]
    [Arguments(StatusCode.DeadlineExceeded)]
    public async Task EverythingElse_IncludingAnUnreachableServer_IsTheGeneralMessage(StatusCode code)
    {
        var errors = CreateUserErrors.From(new RpcException(new GrpcStatus(code, "SQLITE_BUSY at /secret")));

        await Assert.That(errors).IsEqualTo(new CreateUserErrors(General: "CreateFailed"));
    }

    [Test]
    public async Task AnRpcExceptionWrappedByTheTypedClient_IsUnwrapped()
    {
        // Wolverine's typed client maps INVALID_ARGUMENT to an ArgumentException around the original.
        var wrapped = new ArgumentException("The request is invalid.", InvalidArgument(("email", "required")));

        var errors = CreateUserErrors.From(wrapped);

        await Assert.That(errors).IsEqualTo(new CreateUserErrors(Email: "EmailRequired"));
    }

    [Test]
    public async Task ANonRpcException_IsTheGeneralMessage()
    {
        var errors = CreateUserErrors.From(new HttpRequestException("connection refused"));

        await Assert.That(errors).IsEqualTo(new CreateUserErrors(General: "CreateFailed"));
    }

    [Test]
    public async Task EveryKeyItCanProduce_ExistsInTheResourceFile()
    {
        var resources = new ResourceManager(
            "OfficeRkyv.UI.Resources.Components.Pages.Users.CreateUserDialog",
            typeof(CreateUserErrors).Assembly
        );

        var produced = new List<CreateUserErrors>
        {
            CreateUserErrors.From(new RpcException(new GrpcStatus(StatusCode.AlreadyExists, ""))),
            CreateUserErrors.From(new RpcException(new GrpcStatus(StatusCode.Unavailable, ""))),
            CreateUserErrors.From(new InvalidOperationException()),
            CreateUserErrors.From(InvalidArgument(("nickname", "required"))),
        };

        foreach (var field in new[] { "email", "display_name" })
        {
            foreach (var code in new[] { "required", "invalid_format", "too_long", "unknown_code" })
            {
                produced.Add(CreateUserErrors.From(InvalidArgument((field, code))));
            }
        }

        var keys = produced
            .SelectMany(e => new[] { e.Email, e.DisplayName, e.General })
            .OfType<string>()
            .Distinct()
            .ToList();

        await Assert.That(keys).IsNotEmpty();

        foreach (var key in keys)
        {
            await Assert.That(resources.GetString(key, CultureInfo.InvariantCulture)).IsNotNull();
        }
    }
}
