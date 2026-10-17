using Google.Protobuf.WellKnownTypes;
using Riok.Mapperly.Abstractions;

namespace OfficeRkyv.Api.Features.Documents;

[Mapper]
public static partial class DocumentMapper
{
    public static FindInboxQuery ToQuery(this ListInboxRequest request) => new(Guid.Parse(request.OwnerId));

    public static FindArchiveDocumentsQuery ToQuery(this ListArchiveDocumentsRequest request) =>
        new(Guid.Parse(request.ArchiveId));

    public static SearchDocumentsQuery ToQuery(this SearchDocumentsRequest request) =>
        new(Guid.Parse(request.OwnerId), request.Query);

    public static RenameDocumentCommand ToCommand(this RenameDocumentRequest request) =>
        new(Guid.Parse(request.DocumentId), request.Version, request.Title);

    public static DeleteDocumentCommand ToCommand(this DeleteDocumentRequest request) =>
        new(Guid.Parse(request.DocumentId), request.Version);

    public static FileDocumentCommand ToCommand(this FileDocumentRequest request) =>
        new(Guid.Parse(request.DocumentId), request.Version, Guid.Parse(request.ArchiveId));

    public static UnfileDocumentCommand ToCommand(this UnfileDocumentRequest request) =>
        new(Guid.Parse(request.DocumentId), request.Version);

    // The proto field is `optional` and its setter rejects null, so ArchiveId is set only when the
    // document is filed; everything else is mapped by MapCore.
    public static DocumentDto ToDto(this Document document)
    {
        var dto = document.MapCore();

        if (document.ArchiveId is { } archiveId)
        {
            dto.ArchiveId = archiveId.ToString();
        }

        return dto;
    }

    // Status is not exposed: deleted documents are filtered out before they reach the API.
    [MapperIgnoreSource(nameof(Document.Status))]
    [MapperIgnoreSource(nameof(Document.ArchiveId))]
    [MapperIgnoreTarget(nameof(DocumentDto.ArchiveId))]
    private static partial DocumentDto MapCore(this Document document);

    // Hand-written: `repeated` fields are get-only RepeatedField<T>, so items must be added, not assigned.
    public static ListInboxResponse ToInboxResponse(this IEnumerable<Document> documents) =>
        new() { Documents = { documents.Select(ToDto) } };

    public static ListArchiveDocumentsResponse ToArchiveDocumentsResponse(this IEnumerable<Document> documents) =>
        new() { Documents = { documents.Select(ToDto) } };

    public static SearchDocumentsResponse ToSearchResponse(this SearchDocumentsResult result) =>
        new() { Documents = { result.Documents.Select(ToDto) }, HasMore = result.HasMore };

    public static UploadDocumentResponse ToUploadResponse(this Document document) =>
        new() { Document = document.ToDto() };

    public static RenameDocumentResponse ToRenameResponse(this Document document) =>
        new() { Document = document.ToDto() };

    public static FileDocumentResponse ToFileResponse(this Document document) => new() { Document = document.ToDto() };

    public static UnfileDocumentResponse ToUnfileResponse(this Document document) =>
        new() { Document = document.ToDto() };

    private static Timestamp ToTimestamp(DateTimeOffset value) => Timestamp.FromDateTimeOffset(value);
}
