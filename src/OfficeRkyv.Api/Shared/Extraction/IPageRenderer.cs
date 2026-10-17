namespace OfficeRkyv.Api.Shared.Extraction;

/// <summary>Renders one page of a PDF to an image, for a page that has no text layer to read.</summary>
public interface IPageRenderer
{
    /// <summary>
    /// Renders the page to a PNG file and returns its path, or returns null when the page could not be rendered:
    /// the program is not installed, failed, or ran out of time. The caller deletes the file.
    /// </summary>
    /// <param name="sha256">The content the page is of; named in the log when the program fails, never its output.</param>
    /// <param name="pdfPath">The path of the stored PDF.</param>
    /// <param name="pageNumber">The page, counting from 1.</param>
    /// <param name="imagePathWithoutExtension">Where to write the image; the program adds <c>.png</c>.</param>
    Task<string?> RenderAsync(
        string sha256,
        string pdfPath,
        int pageNumber,
        string imagePathWithoutExtension,
        CancellationToken cancellation = default
    );
}
