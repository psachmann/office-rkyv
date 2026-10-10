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

    private ArchivesService.ArchivesServiceClient Archives => new(pipeline.Channel);

    private DocumentsService.DocumentsServiceClient Documents => new(pipeline.Channel);

    [Before(Test)]
    public async Task SetUp() => pipeline = await PipelineHost.StartAsync();

    private async Task<UserDto> CreateUser(string email = "alice@example.com") =>
        (await Users.CreateUserAsync(new CreateUserRequest { Email = email, DisplayName = email })).User;

    private async Task<ArchiveDto> CreateArchive(UserDto owner, string name = "Taxes") =>
        (await Archives.CreateArchiveAsync(new CreateArchiveRequest { OwnerId = owner.Id, Name = name })).Archive;

    private Task<DocumentDto> CreateDocument(UserDto owner, string title = "Invoice") =>
        TestDocuments.UploadAsync(Documents, owner.Id, $"{title}.pdf", TestDocuments.Bytes(10), title: title);

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
        await Users.DeleteUserAsync(new DeleteUserRequest { UserId = alice.Id, Version = alice.Version });

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
            await Archives.RenameArchiveAsync(
                new RenameArchiveRequest
                {
                    ArchiveId = created.Id,
                    Version = created.Version,
                    Name = "Taxes 2026",
                }
            )
        ).Archive;
        var listed = (await Archives.ListArchivesAsync(new ListArchivesRequest { OwnerId = alice.Id })).Archives;

        await Assert.That(created.OwnerId).IsEqualTo(alice.Id);
        await Assert.That(renamed.Name).IsEqualTo("Taxes 2026");
        await Assert.That(renamed.Version).IsGreaterThan(created.Version);
        await Assert.That(listed.Select(archive => archive.Name)).IsEquivalentTo(["Taxes 2026"]);

        await Archives.DeleteArchiveAsync(
            new DeleteArchiveRequest { ArchiveId = created.Id, Version = renamed.Version }
        );

        await Assert
            .That((await Archives.ListArchivesAsync(new ListArchivesRequest { OwnerId = alice.Id })).Archives)
            .IsEmpty();
    }

    [Test]
    public async Task Archives_RenamingADeletedArchive_IsFailedPrecondition_WithAReason()
    {
        var alice = await CreateUser();
        var created = await CreateArchive(alice);
        await Archives.DeleteArchiveAsync(
            new DeleteArchiveRequest { ArchiveId = created.Id, Version = created.Version }
        );

        var failure = await GrpcFailure.Of(async () =>
            await Archives.RenameArchiveAsync(
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
            await Archives.CreateArchiveAsync(new CreateArchiveRequest { OwnerId = alice.Id, Name = "  " })
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
        await Archives.RenameArchiveAsync(
            new RenameArchiveRequest
            {
                ArchiveId = created.Id,
                Version = created.Version,
                Name = "Newer",
            }
        );

        var failure = await GrpcFailure.Of(async () =>
            await Archives.RenameArchiveAsync(
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
            .That(
                (await Documents.ListInboxAsync(new ListInboxRequest { OwnerId = alice.Id })).Documents.Select(d =>
                    d.Id
                )
            )
            .IsEquivalentTo([created.Id]);

        var filed = (
            await Documents.FileDocumentAsync(
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
                    await Documents.ListArchiveDocumentsAsync(
                        new ListArchiveDocumentsRequest { ArchiveId = archive.Id }
                    )
                ).Documents.Select(d => d.Id)
            )
            .IsEquivalentTo([created.Id]);

        var unfiled = (
            await Documents.UnfileDocumentAsync(
                new UnfileDocumentRequest { DocumentId = created.Id, Version = filed.Version }
            )
        ).Document;
        await Assert.That(unfiled.HasArchiveId).IsFalse();

        var renamed = (
            await Documents.RenameDocumentAsync(
                new RenameDocumentRequest
                {
                    DocumentId = created.Id,
                    Version = unfiled.Version,
                    Title = "Invoice April",
                }
            )
        ).Document;
        await Assert.That(renamed.Title).IsEqualTo("Invoice April");

        await Documents.DeleteDocumentAsync(
            new DeleteDocumentRequest { DocumentId = created.Id, Version = renamed.Version }
        );
        await Assert
            .That((await Documents.ListInboxAsync(new ListInboxRequest { OwnerId = alice.Id })).Documents)
            .IsEmpty();
    }

    [Test]
    public async Task Documents_FilingIntoAnotherUsersArchive_IsFailedPrecondition_WithAReason()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        var bobsArchive = await CreateArchive(bob);
        var document = await CreateDocument(alice);

        var failure = await GrpcFailure.Of(async () =>
            await Documents.FileDocumentAsync(
                new FileDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    ArchiveId = bobsArchive.Id,
                }
            )
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.FailedPrecondition);
        await Assert.That(GrpcFailure.Reason(failure)).IsEqualTo(InvariantCodes.ArchiveOwnerMismatch);
    }

    [Test]
    public async Task Documents_FilingIntoAnUnknownArchive_IsNotFound()
    {
        var alice = await CreateUser();
        var document = await CreateDocument(alice);

        var failure = await GrpcFailure.Of(async () =>
            await Documents.FileDocumentAsync(
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
            await TestDocuments.UploadAsync(Documents, alice.Id, "a.pdf", TestDocuments.Bytes(3), title: "")
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert
            .That(GrpcFailure.Detail<BadRequest>(failure).FieldViolations.Select(v => (v.Field, v.Reason)))
            .IsEquivalentTo([("title", ViolationCodes.Required)]);
    }

    [Test]
    public async Task Documents_ForADeletedOwner_IsFailedPrecondition_WithUserDeleted()
    {
        var alice = await CreateUser();
        await Users.DeleteUserAsync(new DeleteUserRequest { UserId = alice.Id, Version = alice.Version });

        var failure = await GrpcFailure.Of(async () => await CreateDocument(alice));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.FailedPrecondition);
        await Assert.That(GrpcFailure.Reason(failure)).IsEqualTo(InvariantCodes.UserDeleted);
    }

    [Test]
    public async Task DeletingAnArchive_ReturnsItsDocumentsToTheInbox_Shortly()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice, "Household");
        var document = await CreateDocument(alice);
        await Documents.FileDocumentAsync(
            new FileDocumentRequest
            {
                DocumentId = document.Id,
                Version = document.Version,
                ArchiveId = archive.Id,
            }
        );
        var current = (
            await Archives.ListArchivesAsync(new ListArchivesRequest { OwnerId = alice.Id })
        ).Archives.Single();

        await Archives.DeleteArchiveAsync(
            new DeleteArchiveRequest { ArchiveId = archive.Id, Version = current.Version }
        );

        // The cascade is asynchronous, so poll: it must land, but not necessarily before the delete returns.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        IReadOnlyList<string> inbox;
        do
        {
            inbox = (await Documents.ListInboxAsync(new ListInboxRequest { OwnerId = alice.Id }))
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
