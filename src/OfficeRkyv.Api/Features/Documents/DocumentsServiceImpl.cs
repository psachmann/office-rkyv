using System.Runtime.CompilerServices;
using FluentValidation;
using FluentValidation.Results;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OfficeRkyv.Api.Shared.Blobs;
using Wolverine;

namespace OfficeRkyv.Api.Features.Documents;

public partial class DocumentsServiceImpl(
    IMessageBus bus,
    DocumentIngest ingest,
    IBlobStore blobs,
    ILogger<DocumentsServiceImpl> logger
) : DocumentsService.DocumentsServiceBase
{
    private const int ChunkSize = FileLimits.ChunkSize;

    public override async Task<ListInboxResponse> ListInbox(ListInboxRequest request, ServerCallContext context)
    {
        var documents = await bus.InvokeAsync<IReadOnlyList<Document>>(request.ToQuery(), context.CancellationToken);

        return documents.ToInboxResponse();
    }

    public override async Task<ListArchiveDocumentsResponse> ListArchiveDocuments(
        ListArchiveDocumentsRequest request,
        ServerCallContext context
    )
    {
        var documents = await bus.InvokeAsync<IReadOnlyList<Document>>(request.ToQuery(), context.CancellationToken);

        return documents.ToArchiveDocumentsResponse();
    }

    // The one method that does work of its own: the content has to be read, and stored, before there is a
    // command to dispatch. DocumentIngest owns that; this only turns the stream into what it takes.
    public override async Task<UploadDocumentResponse> UploadDocument(
        IAsyncStreamReader<UploadDocumentRequest> requestStream,
        ServerCallContext context
    )
    {
        var cancellation = context.CancellationToken;
        var header = await ReadHeaderAsync(requestStream, cancellation);

        var document = await ingest.IngestAsync(
            bus,
            header,
            ReadChunksAsync(requestStream, cancellation),
            cancellation
        );

        return document.ToUploadResponse();
    }

    public override async Task DownloadDocument(
        DownloadDocumentRequest request,
        IServerStreamWriter<DownloadDocumentResponse> responseStream,
        ServerCallContext context
    )
    {
        var cancellation = context.CancellationToken;
        var documentId = Guid.Parse(request.DocumentId);

        var document =
            await bus.InvokeAsync<Document?>(new FindDocumentByIdQuery(documentId), cancellation)
            ?? throw new NotFoundException($"Document '{documentId}' does not exist.");

        if (document.Status is EntityStatus.Deleted)
        {
            throw new InvariantViolationException(InvariantCodes.DocumentDeleted);
        }

        await using var content = blobs.OpenRead(document.Sha256);

        if (content is null)
        {
            // Not a refusal but a fault: the event store says there is a file and the blob store has none.
            LogContentMissing(logger, documentId);

            throw new InvalidOperationException($"The content of document '{documentId}' is missing.");
        }

        var buffer = new byte[ChunkSize];
        int read;

        while ((read = await content.ReadAsync(buffer, cancellation)) > 0)
        {
            // Copied: the writer may still hold the message while the buffer is refilled.
            await responseStream.WriteAsync(
                new DownloadDocumentResponse { Chunk = ByteString.CopyFrom(buffer, 0, read) },
                cancellation
            );
        }
    }

    public override async Task<RenameDocumentResponse> RenameDocument(
        RenameDocumentRequest request,
        ServerCallContext context
    )
    {
        var command = request.ToCommand();
        await bus.InvokeAsync(command, context.CancellationToken);

        return (await ReadAsync(command.DocumentId, context)).ToRenameResponse();
    }

    public override async Task<Empty> DeleteDocument(DeleteDocumentRequest request, ServerCallContext context)
    {
        await bus.InvokeAsync(request.ToCommand(), context.CancellationToken);

        return new();
    }

    public override async Task<FileDocumentResponse> FileDocument(
        FileDocumentRequest request,
        ServerCallContext context
    )
    {
        var document = await bus.InvokeAsync<Document>(request.ToCommand(), context.CancellationToken);

        return document.ToFileResponse();
    }

    public override async Task<UnfileDocumentResponse> UnfileDocument(
        UnfileDocumentRequest request,
        ServerCallContext context
    )
    {
        var command = request.ToCommand();
        await bus.InvokeAsync(command, context.CancellationToken);

        return (await ReadAsync(command.DocumentId, context)).ToUnfileResponse();
    }

    // Inline projection: the snapshot is already current, so this reads the result of the write
    // that just happened rather than racing a background projector.
    private async Task<Document> ReadAsync(Guid documentId, ServerCallContext context) =>
        (await bus.InvokeAsync<Document?>(new FindDocumentByIdQuery(documentId), context.CancellationToken))!;

    private static async Task<UploadHeader> ReadHeaderAsync(
        IAsyncStreamReader<UploadDocumentRequest> requestStream,
        CancellationToken cancellation
    )
    {
        if (
            !await requestStream.MoveNext(cancellation)
            || requestStream.Current.PayloadCase is not UploadDocumentRequest.PayloadOneofCase.Header
        )
        {
            throw Malformed(ViolationCodes.Required, "An upload starts with its header.");
        }

        var header = requestStream.Current.Header;

        return new UploadHeader(
            Guid.Parse(header.OwnerId),
            header.FileName,
            header.ContentType,
            header.HasTitle ? header.Title : null
        );
    }

    // Everything after the header is content. The blob store drains this as it writes, and stops at its limit.
    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunksAsync(
        IAsyncStreamReader<UploadDocumentRequest> requestStream,
        [EnumeratorCancellation] CancellationToken cancellation
    )
    {
        while (await requestStream.MoveNext(cancellation))
        {
            if (requestStream.Current.PayloadCase is not UploadDocumentRequest.PayloadOneofCase.Chunk)
            {
                throw Malformed(ViolationCodes.InvalidFormat, "Only the first message of an upload is a header.");
            }

            yield return requestStream.Current.Chunk.Memory;
        }
    }

    private static ValidationException Malformed(string code, string message) =>
        new([new ValidationFailure("Header", message) { ErrorCode = code }]);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The file content of document {DocumentId} is missing from the blob store"
    )]
    private static partial void LogContentMissing(ILogger logger, Guid documentId);
}
