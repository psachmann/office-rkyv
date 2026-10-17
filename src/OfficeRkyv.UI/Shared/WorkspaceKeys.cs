namespace OfficeRkyv.UI.Shared;

/// <summary>
/// The first part of every workspace query's key. A query's full key adds what it was asked about
/// (<c>["inbox", ownerId]</c>), and a mutation invalidates by kind, so it refreshes every query of that
/// kind without knowing which owner or archive the page is showing.
/// </summary>
/// <remarks>
/// <c>MutationOptions.InvalidateKeys</c> cannot do this: it matches whole keys, so <c>["inbox"]</c> would not
/// match <c>["inbox", ownerId]</c>. Invalidating through the client's predicate overload does.
/// </remarks>
internal static class WorkspaceKeys
{
    public const string Archives = "archives";
    public const string Inbox = "inbox";
    public const string ArchiveDocuments = "archive-documents";
    public const string Search = "search";

    public static QueryKey ForArchives(Guid ownerId) => [Archives, ownerId];

    public static QueryKey ForInbox(Guid ownerId) => [Inbox, ownerId];

    public static QueryKey ForArchiveDocuments(Guid archiveId) => [ArchiveDocuments, archiveId];

    public static QueryKey ForSearch(DocumentSearch search) => [Search, search.OwnerId, search.Query];

    /// <summary>Marks every query of the given kinds stale, whatever it was asked about.</summary>
    public static void Invalidate(this IQueryClient queryClient, params string[] kinds) =>
        queryClient.Invalidate(key => key.Parts.Count > 0 && key.Parts[0] is string kind && kinds.Contains(kind));
}
