namespace OfficeRkyv.Api.Shared.Extraction;

/// <summary>Reads the text in an image by OCR.</summary>
public interface IImageReader
{
    /// <summary>
    /// Returns the text in the image, which is empty when there is none, or returns null when the image could not
    /// be read: the program is not installed, failed, or ran out of time.
    /// </summary>
    /// <param name="sha256">The content the page is of; named in the log when the program fails, never its output.</param>
    /// <param name="pageNumber">The page of the content this image is, counting from 1; 1 for an image file.</param>
    /// <param name="imagePath">The path of an image file; its format is recognised from its content.</param>
    Task<string?> ReadAsync(string sha256, int pageNumber, string imagePath, CancellationToken cancellation = default);
}
