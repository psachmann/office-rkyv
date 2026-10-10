using System.Security.Cryptography;
using Google.Protobuf;
using Google.Rpc;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;
using OfficeRkyv.UnitTests.Grpc;
using Wolverine;

namespace OfficeRkyv.UnitTests.Documents;

/// <summary>
/// The file side of a document, as a client sees it: the streaming upload and download through the real
/// service, pipeline and error mapping, over a real blob store in a temporary directory.
/// </summary>
public sealed class DocumentUploadTests : IAsyncDisposable
{
    // Small enough to reach in a test, and not a multiple of the chunk size used below.
    private const int Limit = 1_000;

    private PipelineHost pipeline = null!;

    private DocumentsService.DocumentsServiceClient Documents => new(pipeline.Channel);

    private UsersService.UsersServiceClient Users => new(pipeline.Channel);

    [Before(Test)]
    public async Task SetUp() => pipeline = await PipelineHost.StartAsync(maxFileSizeBytes: Limit);

    private async Task<UserDto> CreateUser(string email = "alice@example.com") =>
        (await Users.CreateUserAsync(new CreateUserRequest { Email = email, DisplayName = email })).User;

    private static string Sha256Of(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    private string BlobTemporaryDirectory => Path.Combine(pipeline.BlobDirectory, "tmp");

    private static (string Field, string Reason) Violation(RpcException failure) =>
        GrpcFailure.Detail<BadRequest>(failure).FieldViolations.Select(v => (v.Field, v.Reason)).Single();

    // ---- creating ----

    [Test]
    public async Task AnUpload_CreatesADocumentInTheInbox_WithTheFilesFacts()
    {
        var alice = await CreateUser();
        var content = TestDocuments.Bytes(600);

        var document = await TestDocuments.UploadAsync(
            Documents,
            alice.Id,
            "scan.pdf",
            content,
            "application/pdf",
            title: "Invoice March",
            chunkSize: 256
        );

        await Assert.That(document.Title).IsEqualTo("Invoice March");
        await Assert.That(document.OwnerId).IsEqualTo(alice.Id);
        await Assert.That(document.HasArchiveId).IsFalse();
        await Assert.That(document.FileName).IsEqualTo("scan.pdf");
        await Assert.That(document.ContentType).IsEqualTo("application/pdf");
        await Assert.That(document.Size).IsEqualTo(600);
        await Assert.That(document.Sha256).IsEqualTo(Sha256Of(content));

        var inbox = await Documents.ListInboxAsync(new ListInboxRequest { OwnerId = alice.Id });
        await Assert.That(inbox.Documents.Select(d => d.Id)).IsEquivalentTo([document.Id]);
        await Assert.That(inbox.Documents.Single().FileName).IsEqualTo("scan.pdf");
        await Assert.That(inbox.Documents.Single().Size).IsEqualTo(600);
    }

    [Test]
    public async Task TheFileFacts_SurviveReplayingTheStream()
    {
        var alice = await CreateUser();
        var content = TestDocuments.Bytes(300);
        var document = await TestDocuments.UploadAsync(Documents, alice.Id, "scan.pdf", content);

        // Rebuilt from the events, not read from the snapshot.
        await using var session = pipeline.Store.QuerySession();
        var replayed = await session.Events.AggregateStreamAsync<Document>(Guid.Parse(document.Id));

        await Assert.That(replayed!.FileName).IsEqualTo("scan.pdf");
        await Assert.That(replayed.Size).IsEqualTo(300);
        await Assert.That(replayed.Sha256).IsEqualTo(Sha256Of(content));
    }

    [Test]
    public async Task WithoutATitle_TheTitleComesFromTheFileName()
    {
        var alice = await CreateUser();

        var document = await TestDocuments.UploadAsync(
            Documents,
            alice.Id,
            "Invoice March.pdf",
            TestDocuments.Bytes(10)
        );

        await Assert.That(document.Title).IsEqualTo("Invoice March");
    }

    [Test]
    public async Task ASuppliedTitle_IsStoredTrimmed()
    {
        var alice = await CreateUser();

        var document = await TestDocuments.UploadAsync(
            Documents,
            alice.Id,
            "a.pdf",
            TestDocuments.Bytes(10),
            title: "  Lease contract  "
        );

        await Assert.That(document.Title).IsEqualTo("Lease contract");
    }

    [Test]
    public async Task APath_IsReducedToItsFileName()
    {
        var alice = await CreateUser();

        var document = await TestDocuments.UploadAsync(
            Documents,
            alice.Id,
            "scans/2026\\invoice.pdf",
            TestDocuments.Bytes(10)
        );

        await Assert.That(document.FileName).IsEqualTo("invoice.pdf");
    }

    [Test]
    public async Task WithoutAContentType_TheContentTypeIsOctetStream()
    {
        var alice = await CreateUser();

        var document = await TestDocuments.UploadAsync(
            Documents,
            alice.Id,
            "a.bin",
            TestDocuments.Bytes(10),
            contentType: null
        );

        await Assert.That(document.ContentType).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task ARename_LeavesTheFileUnchanged()
    {
        var alice = await CreateUser();
        var content = TestDocuments.Bytes(100);
        var document = await TestDocuments.UploadAsync(Documents, alice.Id, "scan.pdf", content);

        var renamed = (
            await Documents.RenameDocumentAsync(
                new RenameDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    Title = "April",
                }
            )
        ).Document;

        await Assert.That(renamed.Title).IsEqualTo("April");
        await Assert.That(renamed.FileName).IsEqualTo("scan.pdf");
        await Assert.That(renamed.ContentType).IsEqualTo(document.ContentType);
        await Assert.That(renamed.Size).IsEqualTo(document.Size);
        await Assert.That(renamed.Sha256).IsEqualTo(document.Sha256);
    }

    // ---- refusals ----

    [Test]
    public async Task AFileAtTheLimit_IsAccepted()
    {
        var alice = await CreateUser();

        var document = await TestDocuments.UploadAsync(
            Documents,
            alice.Id,
            "big.bin",
            TestDocuments.Bytes(Limit),
            chunkSize: 300
        );

        await Assert.That(document.Size).IsEqualTo(Limit);
    }

    [Test]
    public async Task AFileOneByteOverTheLimit_IsTooLarge_AndCreatesNothing()
    {
        var alice = await CreateUser();

        var failure = await GrpcFailure.Of(async () =>
            await TestDocuments.UploadAsync(
                Documents,
                alice.Id,
                "big.bin",
                TestDocuments.Bytes(Limit + 1),
                chunkSize: 300
            )
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert.That(Violation(failure)).IsEqualTo(("content", ViolationCodes.TooLarge));
        await Assert
            .That((await Documents.ListInboxAsync(new ListInboxRequest { OwnerId = alice.Id })).Documents)
            .IsEmpty();
        await Assert.That(Directory.EnumerateFiles(BlobTemporaryDirectory)).IsEmpty();
    }

    [Test]
    public async Task AnEmptyFile_IsRequiredOnContent_AndCreatesNothing()
    {
        var alice = await CreateUser();

        var failure = await GrpcFailure.Of(async () =>
            await TestDocuments.UploadAsync(Documents, alice.Id, "empty.txt", [])
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert.That(Violation(failure)).IsEqualTo(("content", ViolationCodes.Required));
        await Assert
            .That((await Documents.ListInboxAsync(new ListInboxRequest { OwnerId = alice.Id })).Documents)
            .IsEmpty();
    }

    [Test]
    public async Task ABlankFileName_IsRequiredOnFileName()
    {
        var alice = await CreateUser();

        var failure = await GrpcFailure.Of(async () =>
            await TestDocuments.UploadAsync(Documents, alice.Id, "  ", TestDocuments.Bytes(10), title: "Invoice")
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert.That(Violation(failure)).IsEqualTo(("file_name", ViolationCodes.Required));
    }

    [Test]
    public async Task ABlankTitle_IsRequiredOnTitle()
    {
        var alice = await CreateUser();

        var failure = await GrpcFailure.Of(async () =>
            await TestDocuments.UploadAsync(Documents, alice.Id, "a.pdf", TestDocuments.Bytes(10), title: "   ")
        );

        await Assert.That(Violation(failure)).IsEqualTo(("title", ViolationCodes.Required));
    }

    [Test]
    public async Task AnUnknownOwner_IsNotFound_AndCreatesNoDocument()
    {
        var failure = await GrpcFailure.Of(async () =>
            await TestDocuments.UploadAsync(Documents, Guid.NewGuid().ToString(), "a.pdf", TestDocuments.Bytes(10))
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.NotFound);
    }

    [Test]
    public async Task ADeletedOwner_IsRefusedWithUserDeleted()
    {
        var alice = await CreateUser();
        await Users.DeleteUserAsync(new DeleteUserRequest { UserId = alice.Id, Version = alice.Version });

        var failure = await GrpcFailure.Of(async () =>
            await TestDocuments.UploadAsync(Documents, alice.Id, "a.pdf", TestDocuments.Bytes(10))
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.FailedPrecondition);
        await Assert.That(GrpcFailure.Reason(failure)).IsEqualTo(InvariantCodes.UserDeleted);
    }

    [Test]
    public async Task AnUploadThatDoesNotStartWithItsHeader_IsInvalid()
    {
        var failure = await GrpcFailure.Of(async () =>
        {
            using var call = Documents.UploadDocument();
            await call.RequestStream.WriteAsync(
                new UploadDocumentRequest { Chunk = ByteString.CopyFromUtf8("no header") }
            );
            await call.RequestStream.CompleteAsync();
            await call.ResponseAsync;
        });

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert.That(Violation(failure)).IsEqualTo(("header", ViolationCodes.Required));
    }

    // ---- duplicates ----

    [Test]
    public async Task TheSameFileTwice_GivesTwoDocumentsWithOneHash()
    {
        var alice = await CreateUser();
        var content = TestDocuments.Bytes(200);

        var first = await TestDocuments.UploadAsync(Documents, alice.Id, "scan.pdf", content);
        var second = await TestDocuments.UploadAsync(Documents, alice.Id, "scan.pdf", content);

        await Assert.That(second.Id).IsNotEqualTo(first.Id);
        await Assert.That(second.Sha256).IsEqualTo(first.Sha256);
        var inbox = await Documents.ListInboxAsync(new ListInboxRequest { OwnerId = alice.Id });
        await Assert.That(inbox.Documents).Count().IsEqualTo(2);
    }

    [Test]
    public async Task DeletingOneOfTwoDocumentsWithTheSameFile_KeepsTheOtherDownloadable()
    {
        var alice = await CreateUser();
        var content = TestDocuments.Bytes(200);
        var first = await TestDocuments.UploadAsync(Documents, alice.Id, "scan.pdf", content);
        var second = await TestDocuments.UploadAsync(Documents, alice.Id, "scan.pdf", content);

        await Documents.DeleteDocumentAsync(
            new DeleteDocumentRequest { DocumentId = first.Id, Version = first.Version }
        );

        await Assert.That(await TestDocuments.DownloadAsync(Documents, second.Id)).IsEquivalentTo(content);
    }

    // ---- downloading ----

    [Test]
    public async Task ADownload_EqualsTheUpload_ForAFileOfSeveralChunks()
    {
        // Several 64 KB chunks and a partial one, so the server's chunking is exercised and not just one message.
        await using var big = await PipelineHost.StartAsync(maxFileSizeBytes: 1_000_000);
        var client = new DocumentsService.DocumentsServiceClient(big.Channel);
        var users = new UsersService.UsersServiceClient(big.Channel);
        var alice = (
            await users.CreateUserAsync(new CreateUserRequest { Email = "a@example.com", DisplayName = "A" })
        ).User;
        var content = TestDocuments.Bytes(64 * 1024 * 3 + 123);
        var document = await TestDocuments.UploadAsync(client, alice.Id, "scan.pdf", content, chunkSize: 50_000);

        var downloaded = await TestDocuments.DownloadAsync(client, document.Id);

        await Assert.That(downloaded.Length).IsEqualTo(content.Length);
        await Assert.That(Sha256Of(downloaded)).IsEqualTo(document.Sha256);
    }

    [Test]
    public async Task ADownloadOfAnUnknownDocument_IsNotFound_AndDeliversNothing()
    {
        var failure = await GrpcFailure.Of(async () =>
            await TestDocuments.DownloadAsync(Documents, Guid.NewGuid().ToString())
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.NotFound);
    }

    [Test]
    public async Task ADownloadOfADeletedDocument_IsRefusedWithDocumentDeleted()
    {
        var alice = await CreateUser();
        var document = await TestDocuments.UploadAsync(Documents, alice.Id, "scan.pdf", TestDocuments.Bytes(50));
        await Documents.DeleteDocumentAsync(
            new DeleteDocumentRequest { DocumentId = document.Id, Version = document.Version }
        );

        var failure = await GrpcFailure.Of(async () => await TestDocuments.DownloadAsync(Documents, document.Id));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.FailedPrecondition);
        await Assert.That(GrpcFailure.Reason(failure)).IsEqualTo(InvariantCodes.DocumentDeleted);
    }

    [Test]
    public async Task ADocumentWhoseContentIsMissing_IsAnInternalFault_WithoutLeakingDetails()
    {
        var alice = await CreateUser();
        var document = await TestDocuments.UploadAsync(Documents, alice.Id, "scan.pdf", TestDocuments.Bytes(50));
        File.Delete(Path.Combine(pipeline.BlobDirectory, document.Sha256[..2], document.Sha256));

        var failure = await GrpcFailure.Of(async () => await TestDocuments.DownloadAsync(Documents, document.Id));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.Internal);
        await Assert.That(failure.Status.Detail).DoesNotContain(document.Id);
    }

    public async ValueTask DisposeAsync() => await pipeline.DisposeAsync();
}
