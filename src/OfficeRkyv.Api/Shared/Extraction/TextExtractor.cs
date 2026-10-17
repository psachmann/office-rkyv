using OfficeRkyv.Api.Shared.Blobs;
using UglyToad.PdfPig;

namespace OfficeRkyv.Api.Shared.Extraction;

public enum TextExtractionOutcome
{
    /// <summary>The file was read. Pages that could not be read are counted, and the outcome is still this.</summary>
    Extracted,

    /// <summary>The file is neither a PDF nor a supported image.</summary>
    Unsupported,

    /// <summary>The file is a PDF that cannot be opened, or that is encrypted.</summary>
    Unreadable,
}

/// <param name="Pages">The text of each page in file order; a page that was not read is an empty page.</param>
/// <param name="OcrPageCount">How many pages were read by OCR.</param>
/// <param name="UnreadPageCount">How many pages needed OCR and could not be read.</param>
public sealed record ExtractionResult(
    TextExtractionOutcome Outcome,
    IReadOnlyList<string> Pages,
    int OcrPageCount = 0,
    int UnreadPageCount = 0
)
{
    public int PageCount => Pages.Count;

    public int CharacterCount => Pages.Sum(page => page.Length);
}

/// <summary>
/// Reads the text of a stored file: a PDF page by page, from its text layer where it has one and by OCR where it
/// has not, and an image by OCR. Whether OCR is available is not its business: a page the OCR programs do not
/// read is counted as unread and the rest of the file is still extracted.
/// </summary>
public sealed partial class TextExtractor(
    FileSystemBlobStore blobs,
    IPageRenderer renderer,
    IImageReader reader,
    ILogger<TextExtractor> logger
)
{
    private static readonly ExtractionResult NoText = new(TextExtractionOutcome.Unreadable, []);

    /// <summary>Extracts the text of the content with this hash, or returns null when it is not in the blob store.</summary>
    public async Task<ExtractionResult?> ExtractAsync(string sha256, CancellationToken cancellation = default)
    {
        var path = blobs.PathOfStored(sha256);

        if (path is null)
        {
            LogContentMissing(logger, sha256);

            return null;
        }

        return await ReadLeadingBytes(path, cancellation) switch
        {
            ContentKind.Pdf => await ExtractPdfAsync(sha256, path, cancellation),
            ContentKind.Unsupported => new ExtractionResult(TextExtractionOutcome.Unsupported, []),
            _ => await ExtractImageAsync(sha256, path, cancellation),
        };
    }

    private static async Task<ContentKind> ReadLeadingBytes(string path, CancellationToken cancellation)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1);
        var leading = new byte[ContentKinds.SignatureLength];
        var length = await stream.ReadAtLeastAsync(leading, leading.Length, throwOnEndOfStream: false, cancellation);

        return ContentKinds.Detect(leading.AsSpan(0, length));
    }

    private async Task<ExtractionResult> ExtractImageAsync(string sha256, string path, CancellationToken cancellation)
    {
        var text = await reader.ReadAsync(sha256, 1, path, cancellation);

        return text is null
            ? new ExtractionResult(TextExtractionOutcome.Extracted, [""], UnreadPageCount: 1)
            : new ExtractionResult(TextExtractionOutcome.Extracted, [text], OcrPageCount: 1);
    }

    private async Task<ExtractionResult> ExtractPdfAsync(string sha256, string path, CancellationToken cancellation)
    {
        // PdfPig reads the whole file into memory and throws a variety of exceptions on a file it does not
        // understand, so there is no narrower exception to catch than "it could not be opened".
        PdfDocument document;

        try
        {
            document = PdfDocument.Open(path, new ParsingOptions { SkipMissingFonts = true });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return NoText;
        }

        using (document)
        {
            if (document.IsEncrypted || document.NumberOfPages == 0)
            {
                return NoText;
            }

            var pages = new List<string>(document.NumberOfPages);
            var ocr = 0;
            var unread = 0;

            for (var number = 1; number <= document.NumberOfPages; number++)
            {
                var layer = TextLayerOf(document, number);

                if (TextLayerRule.IsUsable(layer))
                {
                    pages.Add(layer!);

                    continue;
                }

                var text = await ReadByOcrAsync(sha256, path, number, cancellation);

                if (text is null)
                {
                    unread++;
                    pages.Add("");
                }
                else
                {
                    ocr++;
                    pages.Add(text);
                }
            }

            return new ExtractionResult(TextExtractionOutcome.Extracted, pages, ocr, unread);
        }
    }

    // A page PdfPig cannot read is treated as a page without a text layer.
    private static string? TextLayerOf(PdfDocument document, int number)
    {
        try
        {
            return document.GetPage(number).Text;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<string?> ReadByOcrAsync(
        string sha256,
        string pdfPath,
        int number,
        CancellationToken cancellation
    )
    {
        var imagePrefix = blobs.NewTemporaryPath();
        var image = imagePrefix + ".png";

        try
        {
            return await renderer.RenderAsync(sha256, pdfPath, number, imagePrefix, cancellation) is { } rendered
                ? await reader.ReadAsync(sha256, number, rendered, cancellation)
                : null;
        }
        finally
        {
            // The page image is only needed for the read; it is not part of the stored data.
            File.Delete(image);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The content {Sha256} is missing from the blob store, so its text was not extracted"
    )]
    private static partial void LogContentMissing(ILogger logger, string sha256);
}
