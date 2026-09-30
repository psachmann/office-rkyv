using System.Globalization;
using System.Resources;
using Google.Rpc;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;
using OfficeRkyv.UI.Components.Pages.Users;
using Wolverine.Grpc.Client;
using GrpcStatus = Grpc.Core.Status;
using ProtoAny = Google.Protobuf.WellKnownTypes.Any;

namespace OfficeRkyv.UnitTests.Web;

public sealed class WorkspaceErrorsTests
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
                    Description = "Developer text the UI must never show.",
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

    private static RpcException FailedPrecondition(string reason) =>
        new Google.Rpc.Status
        {
            Code = (int)Code.FailedPrecondition,
            Message = "The resource's current state does not allow this operation.",
            Details = { ProtoAny.Pack(new ErrorInfo { Reason = reason, Domain = ErrorDomain.Value }) },
        }.ToRpcException();

    [Test]
    public async Task AStaleWrite_IsTheChangedElsewhereMessage_AndAsksForAReload()
    {
        var errors = WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.Aborted, "server text")));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ChangedElsewhere", ReloadNeeded: true));
    }

    [Test]
    public async Task NotFound_IsTheGoneMessage_AndAsksForAReload()
    {
        var errors = WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.NotFound, "server text")));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "NotFound", ReloadNeeded: true));
    }

    [Test]
    [Arguments(InvariantCodes.UserDeleted, "UserDeleted", false)]
    [Arguments(InvariantCodes.ArchiveDeleted, "ArchiveDeleted", true)]
    [Arguments(InvariantCodes.DocumentDeleted, "DocumentDeleted", true)]
    [Arguments(InvariantCodes.ArchiveOwnerMismatch, "ArchiveOwnerMismatch", false)]
    [Arguments("a_rule_added_later", "ActionFailed", false)]
    public async Task FailedPrecondition_IsChosenByItsReason(string reason, string key, bool reload)
    {
        var errors = WorkspaceErrors.From(FailedPrecondition(reason));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: key, ReloadNeeded: reload));
    }

    [Test]
    public async Task FailedPrecondition_WithoutDetails_IsTheGeneralMessage()
    {
        var errors = WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.FailedPrecondition, "bad")));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ActionFailed"));
    }

    [Test]
    [Arguments("name", "required", "NameRequired")]
    [Arguments("name", "too_long", "NameTooLong")]
    [Arguments("name", "something_new", "FieldInvalid")]
    public async Task InvalidArgument_MapsNameViolations(string field, string code, string expected)
    {
        var errors = WorkspaceErrors.From(InvalidArgument((field, code)));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(Name: expected));
    }

    [Test]
    [Arguments("required", "TitleRequired")]
    [Arguments("too_long", "TitleTooLong")]
    [Arguments("something_new", "FieldInvalid")]
    public async Task InvalidArgument_MapsTitleViolations(string code, string expected)
    {
        var errors = WorkspaceErrors.From(InvalidArgument(("title", code)));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(Title: expected));
    }

    [Test]
    public async Task InvalidArgument_KeepsTheFirstViolationPerField()
    {
        var errors = WorkspaceErrors.From(InvalidArgument(("title", "too_long"), ("title", "required")));

        await Assert.That(errors.Title).IsEqualTo("TitleTooLong");
    }

    [Test]
    public async Task InvalidArgument_AboutAFieldTheDialogDoesNotHave_BecomesTheGeneralMessage()
    {
        var errors = WorkspaceErrors.From(InvalidArgument(("nickname", "required")));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ActionFailed"));
    }

    [Test]
    [Arguments(StatusCode.Unavailable)]
    [Arguments(StatusCode.Internal)]
    [Arguments(StatusCode.AlreadyExists)]
    public async Task EverythingElse_IncludingAnUnreachableServer_IsTheGeneralMessage(StatusCode code)
    {
        var errors = WorkspaceErrors.From(new RpcException(new GrpcStatus(code, "SQLITE_BUSY at /secret")));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ActionFailed"));
    }

    [Test]
    public async Task AnRpcExceptionWrappedByTheTypedClient_IsUnwrapped()
    {
        var wrapped = new InvalidOperationException(
            "The state does not allow this.",
            FailedPrecondition(InvariantCodes.ArchiveDeleted)
        );

        await Assert
            .That(WorkspaceErrors.From(wrapped))
            .IsEqualTo(new WorkspaceErrors(General: "ArchiveDeleted", ReloadNeeded: true));
    }

    [Test]
    public async Task ANonRpcException_IsTheGeneralMessage()
    {
        await Assert
            .That(WorkspaceErrors.From(new HttpRequestException("connection refused")))
            .IsEqualTo(new WorkspaceErrors(General: "ActionFailed"));
    }

    [Test]
    public async Task EveryKeyItCanProduce_ExistsInTheResourceFile()
    {
        var resources = new ResourceManager(
            "OfficeRkyv.UI.Resources.Components.Pages.Users.WorkspaceErrors",
            typeof(WorkspaceErrors).Assembly
        );

        var produced = new List<WorkspaceErrors>
        {
            WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.Aborted, ""))),
            WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.NotFound, ""))),
            WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.Unavailable, ""))),
            WorkspaceErrors.From(new InvalidOperationException()),
            WorkspaceErrors.From(InvalidArgument(("nickname", "required"))),
            WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.FailedPrecondition, ""))),
        };

        foreach (
            var reason in new[]
            {
                InvariantCodes.UserDeleted,
                InvariantCodes.ArchiveDeleted,
                InvariantCodes.DocumentDeleted,
                InvariantCodes.ArchiveOwnerMismatch,
                "unknown_reason",
            }
        )
        {
            produced.Add(WorkspaceErrors.From(FailedPrecondition(reason)));
        }

        foreach (var field in new[] { "name", "title" })
        {
            foreach (var code in new[] { "required", "too_long", "unknown_code" })
            {
                produced.Add(WorkspaceErrors.From(InvalidArgument((field, code))));
            }
        }

        var keys = produced
            .SelectMany(error => new[] { error.Name, error.Title, error.General })
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

/// <summary>
/// The mapper against real refusals, as they arrive through the Web's own typed client - the unwrapping of
/// each status code is Wolverine's, so it is checked rather than assumed.
/// </summary>
public sealed class WorkspaceErrorsOverTheWireTests : IAsyncDisposable
{
    private PipelineHost pipeline = null!;
    private ServiceProvider web = null!;

    [Before(Test)]
    public async Task SetUp()
    {
        pipeline = await PipelineHost.StartAsync();

        // Registered as the Web registers them, in a container with no Wolverine runtime - like the Web.
        web = new ServiceCollection()
            .AddLogging()
            .AddWolverineGrpcClient<UsersService.UsersServiceClient>(options => options.Address = pipeline.Address)
            .Services.AddWolverineGrpcClient<ArchivesService.ArchivesServiceClient>(options =>
                options.Address = pipeline.Address
            )
            .Services.AddWolverineGrpcClient<DocumentsService.DocumentsServiceClient>(options =>
                options.Address = pipeline.Address
            )
            .Services.BuildServiceProvider();
    }

    private UsersService.UsersServiceClient Users => web.GetRequiredService<UsersService.UsersServiceClient>();

    private ArchivesService.ArchivesServiceClient Archives =>
        web.GetRequiredService<ArchivesService.ArchivesServiceClient>();

    private DocumentsService.DocumentsServiceClient Documents =>
        web.GetRequiredService<DocumentsService.DocumentsServiceClient>();

    private static async Task<WorkspaceErrors> ErrorOf(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (Exception exception)
        {
            return WorkspaceErrors.From(exception);
        }

        throw new InvalidOperationException("Expected the call to be refused.");
    }

    private async Task<UserDto> CreateUser(string email = "alice@example.com") =>
        (await Users.CreateUserAsync(new CreateUserRequest { Email = email, DisplayName = email })).User;

    [Test]
    public async Task AStaleWrite_ReachesTheUiAsChangedElsewhere()
    {
        var alice = await CreateUser();
        var created = (
            await Documents.CreateDocumentAsync(new CreateDocumentRequest { OwnerId = alice.Id, Title = "Invoice" })
        ).Document;
        await Documents.RenameDocumentAsync(
            new RenameDocumentRequest
            {
                DocumentId = created.Id,
                Version = created.Version,
                Title = "Newer",
            }
        );

        var errors = await ErrorOf(async () =>
            await Documents.RenameDocumentAsync(
                new RenameDocumentRequest
                {
                    DocumentId = created.Id,
                    Version = created.Version,
                    Title = "Late",
                }
            )
        );

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ChangedElsewhere", ReloadNeeded: true));
    }

    [Test]
    public async Task AnUnknownArchive_ReachesTheUiAsNotFound()
    {
        var alice = await CreateUser();
        var document = (
            await Documents.CreateDocumentAsync(new CreateDocumentRequest { OwnerId = alice.Id, Title = "Invoice" })
        ).Document;

        var errors = await ErrorOf(async () =>
            await Documents.FileDocumentAsync(
                new FileDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    ArchiveId = Guid.NewGuid().ToString(),
                }
            )
        );

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "NotFound", ReloadNeeded: true));
    }

    [Test]
    public async Task ADeletedArchive_ReachesTheUiAsArchiveDeleted()
    {
        var alice = await CreateUser();
        var archive = (
            await Archives.CreateArchiveAsync(new CreateArchiveRequest { OwnerId = alice.Id, Name = "Taxes" })
        ).Archive;
        var document = (
            await Documents.CreateDocumentAsync(new CreateDocumentRequest { OwnerId = alice.Id, Title = "Invoice" })
        ).Document;
        await Archives.DeleteArchiveAsync(
            new DeleteArchiveRequest { ArchiveId = archive.Id, Version = archive.Version }
        );

        var errors = await ErrorOf(async () =>
            await Documents.FileDocumentAsync(
                new FileDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    ArchiveId = archive.Id,
                }
            )
        );

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ArchiveDeleted", ReloadNeeded: true));
    }

    [Test]
    public async Task AnotherUsersArchive_ReachesTheUiAsOwnerMismatch()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        var bobsArchive = (
            await Archives.CreateArchiveAsync(new CreateArchiveRequest { OwnerId = bob.Id, Name = "Work" })
        ).Archive;
        var document = (
            await Documents.CreateDocumentAsync(new CreateDocumentRequest { OwnerId = alice.Id, Title = "Invoice" })
        ).Document;

        var errors = await ErrorOf(async () =>
            await Documents.FileDocumentAsync(
                new FileDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    ArchiveId = bobsArchive.Id,
                }
            )
        );

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ArchiveOwnerMismatch"));
    }

    [Test]
    public async Task ADeletedOwner_ReachesTheUiAsUserDeleted()
    {
        var alice = await CreateUser();
        await Users.DeleteUserAsync(new DeleteUserRequest { UserId = alice.Id, Version = alice.Version });

        var errors = await ErrorOf(async () =>
            await Documents.CreateDocumentAsync(new CreateDocumentRequest { OwnerId = alice.Id, Title = "Invoice" })
        );

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "UserDeleted"));
    }

    [Test]
    public async Task InvalidInput_ReachesTheUiAsFieldMessages()
    {
        var alice = await CreateUser();

        var title = await ErrorOf(async () =>
            await Documents.CreateDocumentAsync(new CreateDocumentRequest { OwnerId = alice.Id, Title = " " })
        );
        var name = await ErrorOf(async () =>
            await Archives.CreateArchiveAsync(
                new CreateArchiveRequest { OwnerId = alice.Id, Name = new string('n', 101) }
            )
        );

        await Assert.That(title).IsEqualTo(new WorkspaceErrors(Title: "TitleRequired"));
        await Assert.That(name).IsEqualTo(new WorkspaceErrors(Name: "NameTooLong"));
    }

    public async ValueTask DisposeAsync()
    {
        await web.DisposeAsync();
        await pipeline.DisposeAsync();
    }
}
