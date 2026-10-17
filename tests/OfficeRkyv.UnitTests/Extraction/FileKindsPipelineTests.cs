using OfficeRkyv.Api.Shared.Extraction;
using OfficeRkyv.Contracts.Documents;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// The scenarios of "An image is read by OCR", "The kind of file is decided from its content" and "Files that are
/// not read are recorded and leave the document untouched" in the <c>document-text-extraction</c> spec.
/// </summary>
public sealed class FileKindsPipelineTests : ExtractionPipelineTestBase
{
    // ---- images ----

    [Test]
    [Arguments("receipt.png", "image/png", 0)]
    [Arguments("receipt.jpg", "image/jpeg", 1)]
    [Arguments("receipt.tif", "image/tiff", 2)]
    public async Task AnImage_IsReadByOcr_AsOnePage(string fileName, string contentType, int kind)
    {
        byte[] image = kind switch
        {
            0 => TestPdfs.Png(),
            1 => TestPdfs.Jpeg(),
            _ => TestPdfs.Tiff(),
        };
        Ocr.TextOfPage[1] = "Kassenbon Summe 12,50";
        var document = await UploadAsync(image, fileName, contentType);

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        await Assert.That(outcome.PageCount).IsEqualTo(1);
        await Assert.That(outcome.OcrPageCount).IsEqualTo(1);
        await Assert.That(outcome.UnreadPageCount).IsEqualTo(0);
        await Assert
            .That(await Pipeline.Texts.ReadPagesAsync(document.Sha256))
            .IsEquivalentTo(["Kassenbon Summe 12,50"]);
        // An image is handed to the reader as it is: nothing is rendered.
        await Assert.That(Ocr.RenderCalls).IsEqualTo(0);
        await Assert.That(Ocr.ReadCalls).IsEqualTo(1);
    }

    // ---- the kind is decided from the content ----

    [Test]
    public async Task APdfUploadedWithoutAContentType_IsExtracted_AndKeepsTheRecordedType()
    {
        var document = await UploadAsync(TestPdfs.WithPages(TestPdfs.InvoiceText), "scan", contentType: null);

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(document.ContentType).IsEqualTo("application/octet-stream");
        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        var inbox = await DocumentsOf(Alice).ListInboxAsync(new ListInboxRequest());
        await Assert.That(inbox.Documents.Single().ContentType).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task AZipArchiveDeclaredAsPdf_IsUnsupported()
    {
        var document = await UploadAsync(TestPdfs.Zip(), "contract.pdf", "application/pdf");

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Unsupported);
        await Assert.That(Ocr.RenderCalls).IsEqualTo(0);
        await Assert.That(Ocr.ReadCalls).IsEqualTo(0);
    }

    [Test]
    public async Task AnImageDeclaredAsOctetStream_IsStillReadByOcr()
    {
        var document = await UploadAsync(TestPdfs.Png(), "photo", "application/octet-stream");

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        await Assert.That(outcome.OcrPageCount).IsEqualTo(1);
    }

    // ---- files that are not read ----

    [Test]
    public async Task AnUnsupportedFile_IsRecorded_KeepsNoText_AndStaysListedAndDownloadable()
    {
        var zip = TestPdfs.Zip();
        var document = await UploadAsync(zip, "archive.zip", "application/zip");

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Unsupported);
        await Assert.That(outcome.PageCount).IsEqualTo(0);
        await Assert.That(outcome.CharacterCount).IsEqualTo(0);
        await Assert.That(await Pipeline.Texts.ReadPagesAsync(document.Sha256)).IsNull();
        var inbox = await DocumentsOf(Alice).ListInboxAsync(new ListInboxRequest());
        await Assert.That(inbox.Documents.Select(listed => listed.Id)).IsEquivalentTo([document.Id]);
        await Assert.That(await TestDocuments.DownloadAsync(DocumentsOf(Alice), document.Id)).IsEquivalentTo(zip);
    }

    [Test]
    public async Task ADamagedPdf_IsUnreadable_KeepsNoText_AndStaysListedInTheInbox()
    {
        var damaged = TestPdfs.Truncated(TestPdfs.WithPages(TestPdfs.InvoiceText, TestPdfs.InvoiceText));
        var document = await UploadAsync(damaged, "invoice.pdf");

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Unreadable);
        await Assert.That(await Pipeline.Texts.ReadPagesAsync(document.Sha256)).IsNull();
        var inbox = await DocumentsOf(Alice).ListInboxAsync(new ListInboxRequest());
        await Assert.That(inbox.Documents.Select(listed => listed.Id)).IsEquivalentTo([document.Id]);
        await Assert.That(await TestDocuments.DownloadAsync(DocumentsOf(Alice), document.Id)).IsEquivalentTo(damaged);
        // A file that cannot be opened is not sent to OCR.
        await Assert.That(Ocr.ReadCalls).IsEqualTo(0);
    }

    [Test]
    public async Task ATextFile_IsUnsupported()
    {
        var document = await UploadAsync("just some words in a text file"u8.ToArray(), "notes.txt", "text/plain");

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Unsupported);
    }
}
