namespace OfficeRkyv.Api.Features.Archives.Handlers;

internal static class ArchiveExistence
{
    /// <summary>
    /// Runs from the handlers' Validate step, before the decider loads the stream and checks the expected
    /// version, so an unknown id is reported as not found rather than as a stale version. A soft-deleted
    /// archive still exists: this passes and the decider applies its own rules.
    /// </summary>
    /// <remarks>
    /// An archive of another user is answered exactly as an unknown one, and before the version is looked at, so
    /// nothing about someone else's archive shows.
    /// </remarks>
    public static async Task RequireAsync(IQuerySession session, Guid archiveId, Guid callerId)
    {
        if (await session.LoadAsync<Archive>(archiveId) is not { } archive || archive.OwnerId != callerId)
        {
            throw new NotFoundException($"Archive '{archiveId}' does not exist.");
        }
    }
}
