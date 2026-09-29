namespace OfficeRkyv.Api.Features.Documents.Handlers;

public static class FindInboxHandler
{
    // The inbox is derived, never stored: the owner's live documents that are in no archive.
    public static Task<IReadOnlyList<Document>> Handle(FindInboxQuery query, IQuerySession session) =>
        session
            .Query<Document>()
            .Where(document =>
                document.OwnerId == query.OwnerId
                && document.ArchiveId == null
                && document.Status != EntityStatus.Deleted
            )
            .ToListAsync();
}

public static class FindArchiveDocumentsHandler
{
    public static Task<IReadOnlyList<Document>> Handle(FindArchiveDocumentsQuery query, IQuerySession session) =>
        session
            .Query<Document>()
            .Where(document => document.ArchiveId == query.ArchiveId && document.Status != EntityStatus.Deleted)
            .ToListAsync();
}
