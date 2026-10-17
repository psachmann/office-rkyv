using Google.Rpc;
using Grpc.Core;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;

namespace OfficeRkyv.UnitTests.Grpc;

/// <summary>
/// The archive, document and user-lookup services as a client sees them: a real call through the real
/// service and error mapping.
/// </summary>
public sealed class WorkspaceServicesTests : IAsyncDisposable
{
    private PipelineHost pipeline = null!;

    private UsersService.UsersServiceClient Users => new(pipeline.Channel);

    // Every call is made as a user: the API knows the owner from the caller, not from the request.
    private ArchivesService.ArchivesServiceClient ArchivesOf(UserDto user) => new(pipeline.ChannelFor(user.Id));

    private DocumentsService.DocumentsServiceClient DocumentsOf(UserDto user) => new(pipeline.ChannelFor(user.Id));

    [Before(Test)]
    public async Task SetUp() => pipeline = await PipelineHost.StartAsync();

    private async Task<UserDto> CreateUser(string email = "alice@example.com") =>
        (await Users.CreateUserAsync(new CreateUserRequest { Email = email, DisplayName = email })).User;

    private async Task<ArchiveDto> CreateArchive(UserDto owner, string name = "Taxes") =>
        (await ArchivesOf(owner).CreateArchiveAsync(new CreateArchiveRequest { Name = name })).Archive;

    private Task<DocumentDto> CreateDocument(UserDto owner, string title = "Invoice") =>
        TestDocuments.UploadAsync(DocumentsOf(owner), $"{title}.pdf", TestDocuments.Bytes(10), title: title);

    // ---- users: find by id ----

    [Test]
    public async Task FindUserById_ReturnsTheUser()
    {
        var alice = await CreateUser();

        var response = await Users.FindUserByIdAsync(new FindUserByIdRequest { UserId = alice.Id });

        await Assert.That(response.User.Email).IsEqualTo("alice@example.com");
        await Assert.That(response.User.DisplayName).IsEqualTo("alice@example.com");
    }

    [Test]
    public async Task FindUserById_ForAnUnknownUser_IsUnsetWithoutAnError()
    {
        var response = await Users.FindUserByIdAsync(new FindUserByIdRequest { UserId = Guid.NewGuid().ToString() });

        await Assert.That(response.User).IsNull();
    }

    [Test]
    public async Task FindUserById_ForADeletedUser_IsUnsetWithoutAnError()
    {
        var alice = await CreateUser();
        await Users.DeleteUserAsync(
            new DeleteUserRequest { UserId = alice.Id, Version = alice.Version },
            PipelineHost.CallOptionsFor(Guid.Parse(alice.Id))
        );

        var response = await Users.FindUserByIdAsync(new FindUserByIdRequest { UserId = alice.Id });

        await Assert.That(response.User).IsNull();
    }

    // ---- archives ----

    [Test]
    public async Task Archives_CreateRenameListDelete()
    {
        var alice = await CreateUser();

        var created = await CreateArchive(alice, "Taxes");
        var renamed = (
            await ArchivesOf(alice)
                .RenameArchiveAsync(
                    new RenameArchiveRequest
                    {
                        ArchiveId = created.Id,
                        Version = created.Version,
                        Name = "Taxes 2026",
                    }
                )
        ).Archive;
        var listed = (await ArchivesOf(alice).ListArchivesAsync(new ListArchivesRequest())).Archives;

        await Assert.That(created.OwnerId).IsEqualTo(alice.Id);
        await Assert.That(renamed.Name).IsEqualTo("Taxes 2026");
        await Assert.That(renamed.Version).IsGreaterThan(created.Version);
        await Assert.That(listed.Select(archive => archive.Name)).IsEquivalentTo(["Taxes 2026"]);

        await ArchivesOf(alice)
            .DeleteArchiveAsync(new DeleteArchiveRequest { ArchiveId = created.Id, Version = renamed.Version });

        await Assert.That((await ArchivesOf(alice).ListArchivesAsync(new ListArchivesRequest())).Archives).IsEmpty();
    }

    [Test]
    public async Task Archives_RenamingADeletedArchive_IsFailedPrecondition_WithAReason()
    {
        var alice = await CreateUser();
        var created = await CreateArchive(alice);
        await ArchivesOf(alice)
            .DeleteArchiveAsync(new DeleteArchiveRequest { ArchiveId = created.Id, Version = created.Version });

        var failure = await GrpcFailure.Of(async () =>
            await ArchivesOf(alice)
                .RenameArchiveAsync(
                    new RenameArchiveRequest
                    {
                        ArchiveId = created.Id,
                        Version = created.Version + 1,
                        Name = "Back",
                    }
                )
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.FailedPrecondition);
        await Assert.That(GrpcFailure.Reason(failure)).IsEqualTo(InvariantCodes.ArchiveDeleted);
        await Assert.That(GrpcFailure.Detail<ErrorInfo>(failure).Domain).IsEqualTo(ErrorDomain.Value);
    }

    [Test]
    public async Task Archives_ABlankName_IsInvalidArgument_WithARequiredViolationForName()
    {
        var alice = await CreateUser();

        var failure = await GrpcFailure.Of(async () =>
            await ArchivesOf(alice).CreateArchiveAsync(new CreateArchiveRequest { Name = "  " })
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert
            .That(GrpcFailure.Detail<BadRequest>(failure).FieldViolations.Select(v => (v.Field, v.Reason)))
            .IsEquivalentTo([("name", ViolationCodes.Required)]);
    }

    [Test]
    public async Task Archives_AStaleVersion_IsAborted()
    {
        var alice = await CreateUser();
        var created = await CreateArchive(alice);
        await ArchivesOf(alice)
            .RenameArchiveAsync(
                new RenameArchiveRequest
                {
                    ArchiveId = created.Id,
                    Version = created.Version,
                    Name = "Newer",
                }
            );

        var failure = await GrpcFailure.Of(async () =>
            await ArchivesOf(alice)
                .RenameArchiveAsync(
                    new RenameArchiveRequest
                    {
                        ArchiveId = created.Id,
                        Version = created.Version,
                        Name = "Late",
                    }
                )
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.Aborted);
    }

    // ---- documents ----

    [Test]
    public async Task Documents_CreateFileUnfileRenameDelete()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);

        var created = await CreateDocument(alice, "Invoice March");
        await Assert.That(created.HasArchiveId).IsFalse();
        await Assert
            .That((await DocumentsOf(alice).ListInboxAsync(new ListInboxRequest())).Documents.Select(d => d.Id))
            .IsEquivalentTo([created.Id]);

        var filed = (
            await DocumentsOf(alice)
                .FileDocumentAsync(
                    new FileDocumentRequest
                    {
                        DocumentId = created.Id,
                        Version = created.Version,
                        ArchiveId = archive.Id,
                    }
                )
        ).Document;
        await Assert.That(filed.ArchiveId).IsEqualTo(archive.Id);
        await Assert
            .That(
                (
                    await DocumentsOf(alice)
                        .ListArchiveDocumentsAsync(new ListArchiveDocumentsRequest { ArchiveId = archive.Id })
                ).Documents.Select(d => d.Id)
            )
            .IsEquivalentTo([created.Id]);

        var unfiled = (
            await DocumentsOf(alice)
                .UnfileDocumentAsync(new UnfileDocumentRequest { DocumentId = created.Id, Version = filed.Version })
        ).Document;
        await Assert.That(unfiled.HasArchiveId).IsFalse();

        var renamed = (
            await DocumentsOf(alice)
                .RenameDocumentAsync(
                    new RenameDocumentRequest
                    {
                        DocumentId = created.Id,
                        Version = unfiled.Version,
                        Title = "Invoice April",
                    }
                )
        ).Document;
        await Assert.That(renamed.Title).IsEqualTo("Invoice April");

        await DocumentsOf(alice)
            .DeleteDocumentAsync(new DeleteDocumentRequest { DocumentId = created.Id, Version = renamed.Version });
        await Assert.That((await DocumentsOf(alice).ListInboxAsync(new ListInboxRequest())).Documents).IsEmpty();
    }

    [Test]
    public async Task Documents_FilingIntoAnotherUsersArchive_IsNotFound_NotAnOwnerMismatch()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        var bobsArchive = await CreateArchive(bob);
        var document = await CreateDocument(alice);

        var failure = await GrpcFailure.Of(async () =>
            await DocumentsOf(alice)
                .FileDocumentAsync(
                    new FileDocumentRequest
                    {
                        DocumentId = document.Id,
                        Version = document.Version,
                        ArchiveId = bobsArchive.Id,
                    }
                )
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.NotFound);
    }

    [Test]
    public async Task Documents_FilingIntoAnUnknownArchive_IsNotFound()
    {
        var alice = await CreateUser();
        var document = await CreateDocument(alice);

        var failure = await GrpcFailure.Of(async () =>
            await DocumentsOf(alice)
                .FileDocumentAsync(
                    new FileDocumentRequest
                    {
                        DocumentId = document.Id,
                        Version = document.Version,
                        ArchiveId = Guid.NewGuid().ToString(),
                    }
                )
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.NotFound);
    }

    [Test]
    public async Task Documents_ABlankTitle_IsInvalidArgument_WithARequiredViolationForTitle()
    {
        var alice = await CreateUser();

        var failure = await GrpcFailure.Of(async () =>
            await TestDocuments.UploadAsync(DocumentsOf(alice), "a.pdf", TestDocuments.Bytes(3), title: "")
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert
            .That(GrpcFailure.Detail<BadRequest>(failure).FieldViolations.Select(v => (v.Field, v.Reason)))
            .IsEquivalentTo([("title", ViolationCodes.Required)]);
    }

    [Test]
    public async Task Documents_ForADeletedCaller_IsUnauthenticated_NotUserDeleted()
    {
        var alice = await CreateUser();
        await Users.DeleteUserAsync(
            new DeleteUserRequest { UserId = alice.Id, Version = alice.Version },
            PipelineHost.CallOptionsFor(Guid.Parse(alice.Id))
        );

        var failure = await GrpcFailure.Of(async () => await CreateDocument(alice));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.Unauthenticated);
    }

    [Test]
    public async Task DeletingAnArchive_ReturnsItsDocumentsToTheInbox_Shortly()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice, "Household");
        var document = await CreateDocument(alice);
        await DocumentsOf(alice)
            .FileDocumentAsync(
                new FileDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    ArchiveId = archive.Id,
                }
            );
        var current = (await ArchivesOf(alice).ListArchivesAsync(new ListArchivesRequest())).Archives.Single();

        await ArchivesOf(alice)
            .DeleteArchiveAsync(new DeleteArchiveRequest { ArchiveId = archive.Id, Version = current.Version });

        // The cascade is asynchronous, so poll: it must land, but not necessarily before the delete returns.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        IReadOnlyList<string> inbox;
        do
        {
            inbox = (await DocumentsOf(alice).ListInboxAsync(new ListInboxRequest()))
                .Documents.Select(d => d.Id)
                .ToList();
            if (inbox.Count > 0)
            {
                break;
            }

            await Task.Delay(50);
        } while (DateTime.UtcNow < deadline);

        await Assert.That(inbox).IsEquivalentTo([document.Id]);
    }

    public async ValueTask DisposeAsync() => await pipeline.DisposeAsync();
}
