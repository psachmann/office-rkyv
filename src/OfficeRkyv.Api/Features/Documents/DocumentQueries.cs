namespace OfficeRkyv.Api.Features.Documents;

public sealed record FindInboxQuery(Guid OwnerId);

public sealed record FindArchiveDocumentsQuery(Guid ArchiveId);

public sealed record FindDocumentByIdQuery(Guid DocumentId);
