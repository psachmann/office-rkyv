namespace OfficeRkyv.Api.Features.Documents.Handlers;

public static class UnfileDocumentHandler
{
    public static Task ValidateAsync(UnfileDocumentCommand command, IQuerySession session) =>
        DocumentExistence.RequireAsync(session, command.DocumentId, command.CallerId);

    // Idempotent: a document already in the inbox stays there and appends nothing.
    [DeciderFunction]
    public static IEnumerable<object> Handle(UnfileDocumentCommand command, Document document)
    {
        if (document.Status is EntityStatus.Deleted)
        {
            throw new InvariantViolationException(InvariantCodes.DocumentDeleted);
        }

        if (document.ArchiveId is not null)
        {
            yield return new DocumentUnfiled();
        }
    }
}
