namespace OfficeRkyv.Api.Features.Documents.Handlers;

public static class DeleteDocumentHandler
{
    public static Task ValidateAsync(DeleteDocumentCommand command, IQuerySession session) =>
        DocumentExistence.RequireAsync(session, command.DocumentId);

    // Idempotent: deleting a deleted document succeeds and appends nothing.
    [DeciderFunction]
    public static IEnumerable<object> Handle(DeleteDocumentCommand command, Document document)
    {
        if (document.Status is EntityStatus.Active)
        {
            yield return new DocumentDeleted();
        }
    }
}
