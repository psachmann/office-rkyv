namespace OfficeRkyv.Api.Features.Archives.Handlers;

internal static class ArchiveExistence
{
    /// <summary>
    /// Runs from the handlers' Validate step, before the decider loads the stream and checks the expected
    /// version, so an unknown id is reported as not found rather than as a stale version. A soft-deleted
    /// archive still exists: this passes and the decider applies its own rules.
    /// </summary>
    public static async Task RequireAsync(IQuerySession session, Guid archiveId)
    {
        if (await session.LoadAsync<Archive>(archiveId) is null)
        {
            throw new NotFoundException($"Archive '{archiveId}' does not exist.");
        }
    }
}
