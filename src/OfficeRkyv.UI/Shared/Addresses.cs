namespace OfficeRkyv.UI.Shared;

/// <summary>The addresses of the pages a signed-in user works in, so the pages, the sidebar and the search dialog build the same ones.</summary>
public static class Addresses
{
    /// <summary>The user's inbox: the documents that are in no archive.</summary>
    public const string Inbox = "/inbox";

    /// <summary>The page of the archive of the given id; the id is as the archive's DTO carries it.</summary>
    public static string Archive(string archiveId) => $"/archives/{archiveId}";
}
