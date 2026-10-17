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
    // A document is only ever filed into an archive of its own owner, so asking for the caller's documents in
    // the archive is empty for anyone else's archive, as it is for an unknown one.
    public static Task<IReadOnlyList<Document>> Handle(FindArchiveDocumentsQuery query, IQuerySession session) =>
        session
            .Query<Document>()
            .Where(document =>
                document.ArchiveId == query.ArchiveId
                && document.OwnerId == query.OwnerId
                && document.Status != EntityStatus.Deleted
            )
            .ToListAsync();
}
