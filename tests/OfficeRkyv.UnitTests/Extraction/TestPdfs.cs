using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// Files for the extraction tests, made in the test: no binary asset is checked in. The PDFs come from PdfPig's
/// document builder, so a page either has a text layer with the words given or has none at all.
/// </summary>
internal static class TestPdfs
{
    /// <summary>A text with at least 20 letters or digits (it has 23), so a page with it has a usable text layer.</summary>
    public const string InvoiceText = "Invoice 4711 for March 2026";

    /// <summary>
    /// A PDF with one page per argument. A page with a text is a page with a text layer; a null or empty text is
    /// a page with nothing on it, as a scanner produces before OCR (its image would be on it).
    /// </summary>
    public static byte[] WithPages(params string?[] pageTexts)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var text in pageTexts)
        {
            var page = builder.AddPage(PageSize.A4);

            if (!string.IsNullOrEmpty(text))
            {
                page.AddText(text, 12, new PdfPoint(50, 700), font);
            }
        }

        return builder.Build();
    }

    /// <summary>A PDF of pages with nothing on them, as a scanner produces before OCR.</summary>
    public static byte[] WithoutText(int pages = 1) => WithPages(new string?[pages]);

    /// <summary>
    /// A PDF whose only content on its one page is the image: a scan, with no text layer. The image is a PNG or a
    /// JPEG.
    /// </summary>
    public static byte[] ScanOf(byte[] image, bool jpeg = false)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var area = new PdfRectangle(0, 0, page.PageSize.Width, page.PageSize.Height);

        if (jpeg)
        {
            page.AddJpeg(image, area);
        }
        else
        {
            page.AddPng(image, area);
        }

        return builder.Build();
    }

    /// <summary>Starts like a PDF and stops in the middle of it.</summary>
    public static byte[] Truncated(byte[] pdf) => pdf[..(pdf.Length / 2)];

    /// <summary>The signature of a ZIP archive, and something after it.</summary>
    public static byte[] Zip() => [0x50, 0x4B, 0x03, 0x04, .. TestDocuments.Bytes(200)];

    /// <summary>Content with a PNG's signature: enough for the kind to be decided; the fakes read it, not a decoder.</summary>
    public static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. TestDocuments.Bytes(200)];

    /// <summary>Content with a JPEG's signature.</summary>
    public static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, .. TestDocuments.Bytes(200)];

    /// <summary>Content with a TIFF's signature, in little-endian byte order.</summary>
    public static byte[] Tiff() => [0x49, 0x49, 0x2A, 0x00, .. TestDocuments.Bytes(200)];
}
