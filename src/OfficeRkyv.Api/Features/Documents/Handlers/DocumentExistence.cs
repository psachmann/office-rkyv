namespace OfficeRkyv.Api.Features.Documents.Handlers;

internal static class DocumentExistence
{
    /// <summary>
    /// Runs from the handlers' Validate step, before the decider loads the stream and checks the expected
    /// version, so an unknown id is reported as not found rather than as a stale version. A soft-deleted
    /// document still exists: this passes and the handler applies its own rules.
    /// </summary>
    public static async Task RequireAsync(IQuerySession session, Guid documentId)
    {
        if (await session.LoadAsync<Document>(documentId) is null)
        {
            throw new NotFoundException($"Document '{documentId}' does not exist.");
        }
    }
}
