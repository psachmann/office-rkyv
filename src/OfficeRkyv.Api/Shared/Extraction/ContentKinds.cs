namespace OfficeRkyv.Api.Shared.Extraction;

public enum ContentKind
{
    Unsupported,
    Pdf,
    Png,
    Jpeg,
    Tiff,
}

public static class ContentKinds
{
    /// <summary>How many leading bytes <see cref="Detect"/> looks at.</summary>
    public const int SignatureLength = 8;

    private static ReadOnlySpan<byte> PdfSignature => "%PDF-"u8;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    // Little-endian ("II", 42) and big-endian ("MM", 42) byte order.
    private static ReadOnlySpan<byte> TiffLittleEndianSignature => [0x49, 0x49, 0x2A, 0x00];

    private static ReadOnlySpan<byte> TiffBigEndianSignature => [0x4D, 0x4D, 0x00, 0x2A];

    /// <summary>
    /// What a file is, decided from its leading bytes and not from the content type recorded with the document,
    /// which is only what the client declared. Anything that is not a PDF or a supported image is
    /// <see cref="ContentKind.Unsupported"/>, including content shorter than a signature.
    /// </summary>
    public static ContentKind Detect(ReadOnlySpan<byte> leadingBytes)
    {
        if (leadingBytes.StartsWith(PdfSignature))
        {
            return ContentKind.Pdf;
        }

        if (leadingBytes.StartsWith(PngSignature))
        {
            return ContentKind.Png;
        }

        if (leadingBytes.StartsWith(JpegSignature))
        {
            return ContentKind.Jpeg;
        }

        if (leadingBytes.StartsWith(TiffLittleEndianSignature) || leadingBytes.StartsWith(TiffBigEndianSignature))
        {
            return ContentKind.Tiff;
        }

        return ContentKind.Unsupported;
    }

    public static bool IsImage(this ContentKind kind) =>
        kind is ContentKind.Png or ContentKind.Jpeg or ContentKind.Tiff;
}
