namespace OfficeRkyv.Api.Features.Documents;

/// <summary>
/// What a document is called when its upload did not say. It lives on the server so that the UI, the seed and
/// a later folder watcher all produce the same titles.
/// </summary>
public static class DocumentTitle
{
    /// <summary>
    /// The file name without its last extension, trimmed and cut to the title limit. A name with nothing left
    /// once the extension is gone (<c>.pdf</c>) is used whole.
    /// </summary>
    public static string FromFileName(string fileName)
    {
        var name = fileName.Trim();
        var dot = name.LastIndexOf('.');
        var stem = (dot > 0 ? name[..dot] : name).Trim();

        return Cut(stem.Length > 0 ? stem : name, Handlers.CreateDocumentValidator.MaxTitleLength);
    }

    private static string Cut(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        // Never end on half of a surrogate pair: the text would no longer be valid UTF-16.
        var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;

        return value[..length].TrimEnd();
    }
}
