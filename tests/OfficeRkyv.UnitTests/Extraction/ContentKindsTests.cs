using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.UnitTests.Extraction;

public sealed class ContentKindsTests
{
    private static byte[] WithTail(params byte[] signature) => [.. signature, .. TestDocuments.Bytes(40)];

    [Test]
    public async Task APdfSignature_IsAPdf()
    {
        await Assert.That(ContentKinds.Detect(WithTail(0x25, 0x50, 0x44, 0x46, 0x2D))).IsEqualTo(ContentKind.Pdf);
        await Assert.That(ContentKinds.Detect("%PDF-1.7\n%âãÏÓ"u8)).IsEqualTo(ContentKind.Pdf);
    }

    [Test]
    public async Task APngSignature_IsAPng()
    {
        await Assert
            .That(ContentKinds.Detect(WithTail(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)))
            .IsEqualTo(ContentKind.Png);
    }

    [Test]
    public async Task AJpegSignature_IsAJpeg()
    {
        await Assert.That(ContentKinds.Detect(WithTail(0xFF, 0xD8, 0xFF, 0xE0))).IsEqualTo(ContentKind.Jpeg);
        await Assert.That(ContentKinds.Detect(WithTail(0xFF, 0xD8, 0xFF, 0xE1))).IsEqualTo(ContentKind.Jpeg);
    }

    [Test]
    public async Task ATiffSignature_IsATiff_InBothByteOrders()
    {
        await Assert.That(ContentKinds.Detect(WithTail(0x49, 0x49, 0x2A, 0x00))).IsEqualTo(ContentKind.Tiff);
        await Assert.That(ContentKinds.Detect(WithTail(0x4D, 0x4D, 0x00, 0x2A))).IsEqualTo(ContentKind.Tiff);
    }

    [Test]
    public async Task AZipSignature_IsUnsupported()
    {
        await Assert.That(ContentKinds.Detect(WithTail(0x50, 0x4B, 0x03, 0x04))).IsEqualTo(ContentKind.Unsupported);
    }

    [Test]
    public async Task ASignatureThatIsCutOff_IsUnsupported()
    {
        await Assert.That(ContentKinds.Detect("%PDF"u8)).IsEqualTo(ContentKind.Unsupported);
        await Assert
            .That(ContentKinds.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A]))
            .IsEqualTo(ContentKind.Unsupported);
        await Assert.That(ContentKinds.Detect([0xFF, 0xD8])).IsEqualTo(ContentKind.Unsupported);
        await Assert.That(ContentKinds.Detect([0x49, 0x49, 0x2A])).IsEqualTo(ContentKind.Unsupported);
    }

    [Test]
    public async Task EmptyContent_IsUnsupported()
    {
        await Assert.That(ContentKinds.Detect([])).IsEqualTo(ContentKind.Unsupported);
    }

    [Test]
    public async Task TextThatMentionsPdf_IsNotAPdf()
    {
        await Assert.That(ContentKinds.Detect(" %PDF-1.7"u8)).IsEqualTo(ContentKind.Unsupported);
    }

    [Test]
    [Arguments(ContentKind.Png, true)]
    [Arguments(ContentKind.Jpeg, true)]
    [Arguments(ContentKind.Tiff, true)]
    [Arguments(ContentKind.Pdf, false)]
    [Arguments(ContentKind.Unsupported, false)]
    public async Task OnlyTheThreeImageKinds_AreImages(ContentKind kind, bool isImage)
    {
        await Assert.That(kind.IsImage()).IsEqualTo(isImage);
    }
}
