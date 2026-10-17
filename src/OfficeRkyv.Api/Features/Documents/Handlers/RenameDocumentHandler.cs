namespace OfficeRkyv.Api.Features.Documents.Handlers;

public static class RenameDocumentHandler
{
    public static Task ValidateAsync(RenameDocumentCommand command, IQuerySession session) =>
        DocumentExistence.RequireAsync(session, command.DocumentId, command.CallerId);

    [DeciderFunction]
    public static IEnumerable<object> Handle(RenameDocumentCommand command, Document document)
    {
        if (document.Status is EntityStatus.Deleted)
        {
            throw new InvariantViolationException(InvariantCodes.DocumentDeleted);
        }

        var title = command.Title.Trim();

        if (document.Title != title)
        {
            yield return new DocumentRenamed(title);
        }
    }
}
