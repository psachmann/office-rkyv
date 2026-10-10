using System.Runtime.CompilerServices;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using OfficeRkyv.Api.Shared.Blobs;
using Wolverine;

namespace OfficeRkyv.Api.Features.Documents;

/// <param name="OwnerId">The user the document will belong to.</param>
/// <param name="FileName">As the client named it; only its last path segment is kept.</param>
/// <param name="ContentType">As the client declared it; null or blank means none was declared.</param>
/// <param name="Title">Null means the server derives it from the file name.</param>
public sealed record UploadHeader(Guid OwnerId, string FileName, string? ContentType, string? Title);

/// <summary>
/// The one way a file becomes a document: the upload RPC, the development seed and a later folder watcher all
/// come through here. The content is stored first and the document created after, so a crash in between
/// leaves an unreferenced blob, never a document without its file.
/// </summary>
/// <remarks>
/// An upload cannot be a plain command, because the file's facts (size, hash) are only known once the content
/// has been read. From the command on it is the ordinary path: validators, then the handler, which checks the
/// owner. The content limits are refused here as validation failures, since no command exists to validate.
/// </remarks>
public sealed class DocumentIngest(IBlobStore blobs, IOptions<FileStorageOptions> options)
{
    public const string DefaultContentType = "application/octet-stream";

    public async Task<Document> IngestAsync(
        IMessageBus bus,
        UploadHeader header,
        IAsyncEnumerable<ReadOnlyMemory<byte>> content,
        CancellationToken cancellation = default
    )
    {
        var result = await blobs.WriteAsync(content, options.Value.MaxFileSizeBytes, cancellation);

        switch (result.Status)
        {
            case BlobWriteStatus.Empty:
                throw Refusal("Content", ViolationCodes.Required, "The file has no content.");
            case BlobWriteStatus.TooLarge:
                throw Refusal(
                    "Content",
                    ViolationCodes.TooLarge,
                    $"The file is larger than {options.Value.MaxFileSizeBytes} bytes."
                );
        }

        var fileName = LastSegment(header.FileName);
        var contentType = string.IsNullOrWhiteSpace(header.ContentType) ? DefaultContentType : header.ContentType;
        var title = header.Title ?? DocumentTitle.FromFileName(fileName);

        return await bus.InvokeAsync<Document>(
            new CreateDocumentCommand(header.OwnerId, title, fileName, contentType, result.Size, result.Sha256!),
            cancellation
        );
    }

    /// <summary>The content of a stream as chunks, for callers that have a file and not a gRPC stream.</summary>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ChunksOf(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellation = default
    )
    {
        var buffer = new byte[FileLimits.ChunkSize];
        int read;

        while ((read = await stream.ReadAsync(buffer, cancellation)) > 0)
        {
            // A copy: the consumer may keep the chunk while this refills the buffer.
            yield return buffer.AsMemory(0, read).ToArray();
        }
    }

    // A client may send a path (a browser never does, a folder watcher might); the name is what is after it.
    private static string LastSegment(string fileName)
    {
        var trimmed = fileName.Trim();

        return trimmed[(trimmed.LastIndexOfAny(['/', '\\']) + 1)..].Trim();
    }

    private static ValidationException Refusal(string property, string code, string message) =>
        new([new ValidationFailure(property, message) { ErrorCode = code }]);
}
