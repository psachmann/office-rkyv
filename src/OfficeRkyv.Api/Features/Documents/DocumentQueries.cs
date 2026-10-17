namespace OfficeRkyv.Api.Features.Documents;

// OwnerId is the caller in the queries that have one, filled in by the service.
public sealed record FindInboxQuery(Guid OwnerId);

// OwnerId is the caller: another user's archive has no documents for them.
public sealed record FindArchiveDocumentsQuery(Guid ArchiveId, Guid OwnerId);

public sealed record FindDocumentByIdQuery(Guid DocumentId);

public sealed record SearchDocumentsQuery(Guid OwnerId, string Query);

/// <summary>The best matches, at most <c>SearchDocumentsHandler.Limit</c>, and whether more matched.</summary>
public sealed record SearchDocumentsResult(IReadOnlyList<Document> Documents, bool HasMore);
