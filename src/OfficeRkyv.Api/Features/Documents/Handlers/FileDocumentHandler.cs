namespace OfficeRkyv.Api.Features.Documents.Handlers;

public static class FileDocumentHandler
{
    /// <summary>
    /// Files a document into an archive. It spans two streams, so it uses the session directly instead of
    /// the single-aggregate decider workflow: the document (at the client's expected version) and the target
    /// archive are both fetched for writing.
    /// </summary>
    /// <remarks>
    /// Filing also appends <see cref="Archives.ArchiveMembershipChanged"/> to the archive. That moves the
    /// archive's version, which is what makes a filing racing with the archive's deletion lose on the
    /// ordinary expected-version check (design D6): whichever commits second aborts, so a document is never
    /// left in a deleted archive.
    /// </remarks>
    public static async Task<Document> Handle(FileDocumentCommand command, IDocumentSession session)
    {
        // Before the version check, so an unknown id is not found rather than a stale version.
        await DocumentExistence.RequireAsync(session, command.DocumentId);

        var documentStream = await session.Events.FetchForWriting<Document>(command.DocumentId, command.Version);
        var document = documentStream.Aggregate!;

        if (document.Status is EntityStatus.Deleted)
        {
            throw new InvariantViolationException(InvariantCodes.DocumentDeleted);
        }

        var archiveStream = await session.Events.FetchForWriting<Archive>(command.ArchiveId);
        var archive =
            archiveStream.Aggregate ?? throw new NotFoundException($"Archive '{command.ArchiveId}' does not exist.");

        if (archive.Status is EntityStatus.Deleted)
        {
            throw new InvariantViolationException(InvariantCodes.ArchiveDeleted);
        }

        if (archive.OwnerId != document.OwnerId)
        {
            throw new InvariantViolationException(InvariantCodes.ArchiveOwnerMismatch);
        }

        if (document.ArchiveId != archive.Id)
        {
            documentStream.AppendOne(new DocumentFiled(archive.Id));
            archiveStream.AppendOne(new ArchiveMembershipChanged());
            await session.SaveChangesAsync();
        }

        return await session.LoadAsync<Document>(command.DocumentId)
            ?? throw new InvalidOperationException($"Document stream '{command.DocumentId}' produced no snapshot.");
    }
}
