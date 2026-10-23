namespace OfficeRkyv.UI.Shared;

/// <summary>The addresses of the workspace's entries, so the page and the search dialog build the same ones.</summary>
public static class WorkspaceAddresses
{
    /// <summary>The workspace with the inbox selected.</summary>
    public const string Inbox = "/workspace";

    /// <summary>The workspace with the archive of the given id selected; the id is as the archive's DTO carries it.</summary>
    public static string Archive(string archiveId) => $"{Inbox}/archives/{archiveId}";
}
