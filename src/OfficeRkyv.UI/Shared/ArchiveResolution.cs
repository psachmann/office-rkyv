using OfficeRkyv.Contracts.Archives;

namespace OfficeRkyv.UI.Shared;

/// <summary>What an archive address means, once the user's archive list has loaded.</summary>
public enum ArchiveLookup
{
    /// <summary>The archive is one of the user's live archives: show it.</summary>
    Found,

    /// <summary>No such archive, and none was shown on this page: the address is wrong, so show not-found.</summary>
    NotFound,

    /// <summary>The archive was shown on this page and is gone now (deleted elsewhere): go to the inbox.</summary>
    Vanished,
}

/// <summary>
/// Decides what an archive page shows for its address. The page calls it only with a loaded list: a list that is
/// loading or failed decides nothing.
/// </summary>
public static class ArchiveResolution
{
    /// <param name="archives">The user's live archives.</param>
    /// <param name="archiveId">The id in the address.</param>
    /// <param name="seenBefore">Whether this archive was shown on the page before it went missing from the list.</param>
    public static ArchiveLookup Resolve(IReadOnlyList<ArchiveDto> archives, Guid archiveId, bool seenBefore)
    {
        if (archives.Any(archive => Guid.TryParse(archive.Id, out var id) && id == archiveId))
        {
            return ArchiveLookup.Found;
        }

        return seenBefore ? ArchiveLookup.Vanished : ArchiveLookup.NotFound;
    }
}
