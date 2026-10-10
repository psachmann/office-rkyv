using Google.Protobuf;
using Grpc.Core;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Contracts.Documents;

namespace OfficeRkyv.UnitTests;

/// <summary>
/// A document is created by uploading a file. Tests that are about something else than the upload (filing,
/// deleting, cascades) still need documents, and make them one of two ways: a command with file facts for a
/// file nobody will read, or a real upload through the generated client.
/// </summary>
internal static class TestDocuments
{
    // Not the hash of any content: a document made this way has no file in the blob store, and a test that
    // downloads one is a test that should upload instead.
    private static readonly string UnreadSha256 = new('0', 64);

    public static CreateDocumentCommand Command(Guid ownerId, string title = "Invoice") =>
        new(ownerId, title, $"{title.Trim()}.pdf", "application/pdf", Size: 3, UnreadSha256);

    /// <summary>One header and the content in chunks of <paramref name="chunkSize"/>, as a client sends it.</summary>
    public static async Task<DocumentDto> UploadAsync(
        DocumentsService.DocumentsServiceClient client,
        string ownerId,
        string fileName,
        byte[] content,
        string? contentType = "application/pdf",
        string? title = null,
        int chunkSize = 64 * 1024
    )
    {
        using var call = client.UploadDocument();
        var header = new UploadDocumentHeader
        {
            OwnerId = ownerId,
            FileName = fileName,
            ContentType = contentType ?? "",
        };

        if (title is not null)
        {
            header.Title = title;
        }

        await call.RequestStream.WriteAsync(new UploadDocumentRequest { Header = header });

        for (var offset = 0; offset < content.Length; offset += chunkSize)
        {
            await call.RequestStream.WriteAsync(
                new UploadDocumentRequest
                {
                    Chunk = ByteString.CopyFrom(content, offset, Math.Min(chunkSize, content.Length - offset)),
                }
            );
        }

        await call.RequestStream.CompleteAsync();

        return (await call.ResponseAsync).Document;
    }

    public static async Task<byte[]> DownloadAsync(DocumentsService.DocumentsServiceClient client, string documentId)
    {
        using var call = client.DownloadDocument(new DownloadDocumentRequest { DocumentId = documentId });
        using var buffer = new MemoryStream();

        await foreach (var message in call.ResponseStream.ReadAllAsync())
        {
            message.Chunk.WriteTo(buffer);
        }

        return buffer.ToArray();
    }

    /// <summary>Bytes that are not all the same, so a test cannot pass by getting the right count of wrong ones.</summary>
    public static byte[] Bytes(int length)
    {
        var bytes = new byte[length];
        new Random(length).NextBytes(bytes);

        return bytes;
    }
}
