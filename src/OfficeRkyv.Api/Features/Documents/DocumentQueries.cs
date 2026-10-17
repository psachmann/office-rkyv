namespace OfficeRkyv.Api.Features.Documents;

public sealed record FindInboxQuery(Guid OwnerId);

public sealed record FindArchiveDocumentsQuery(Guid ArchiveId);

public sealed record FindDocumentByIdQuery(Guid DocumentId);

public sealed record SearchDocumentsQuery(Guid OwnerId, string Query);

/// <summary>The best matches, at most <c>SearchDocumentsHandler.Limit</c>, and whether more matched.</summary>
public sealed record SearchDocumentsResult(IReadOnlyList<Document> Documents, bool HasMore);
