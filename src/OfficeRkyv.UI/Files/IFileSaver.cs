namespace OfficeRkyv.UI.Files;

/// <summary>
/// Hands a downloaded file to the person using the app. The two hosts differ in everything about it (a
/// browser download on the web, a file on the user's disk on the desktop), so the pages depend only on this.
/// </summary>
public interface IFileSaver
{
    /// <summary>
    /// Saves <paramref name="content"/> as <paramref name="fileName"/>, reading it to its end. When the person
    /// cancels, nothing is written and nothing is thrown.
    /// </summary>
    Task<FileSaveResult> SaveAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellation = default
    );
}

/// <param name="Saved">False when the person cancelled and nothing was written.</param>
/// <param name="Location">
/// Where the file went, when the host can say: the page tells the person. A browser has its own download list,
/// so the web has none.
/// </param>
public sealed record FileSaveResult(bool Saved, string? Location = null)
{
    public static FileSaveResult Cancelled { get; } = new(false);
}
