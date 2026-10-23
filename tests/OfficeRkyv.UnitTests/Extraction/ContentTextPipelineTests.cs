using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// The text of an extraction being kept for the search, in the transaction of its outcome: the scenarios of
/// "Extracted text is searchable once its extraction has completed" and "A content is indexed once for all
/// documents that share it" in the <c>document-content-index</c> spec, that are about what is kept. What a search
/// finds is in <c>DocumentContentSearchTests</c>.
/// </summary>
public sealed class ContentTextPipelineTests : ExtractionPipelineTestBase
{
    [Test]
    public async Task AnExtractedPdf_HasItsTextKept_WhenItsOutcomeExists()
    {
        var pdf = TestPdfs.WithPages(TestPdfs.InvoiceText);
        var document = await UploadAsync(pdf);

        // No wait beyond the outcome: the text is written in the transaction that records it.
        await Pipeline.WaitForExtractionAsync(document.Sha256);

        var text = await Pipeline.FindContentTextAsync(document.Sha256);
        await Assert.That(text).IsNotNull();
        await Assert.That(text!.Id).IsEqualTo(document.Sha256);
        await Assert.That(text.Text).Contains(TestPdfs.InvoiceText);
    }

    [Test]
    public async Task APageThatWasNotRead_AddsNothingToTheKeptText()
    {
        Ocr.TextOfPage[1] = "Heizkostenabrechnung";
        Ocr.TextOfPage[3] = "Nachzahlung 120 Euro";
        Ocr.FailOnPages.Add(2);
        var document = await UploadAsync(TestPdfs.WithoutText(3));
        await Pipeline.WaitForExtractionAsync(document.Sha256);

        var text = await Pipeline.FindContentTextAsync(document.Sha256);

        await Assert.That(text!.Text).IsEqualTo("Heizkostenabrechnung\nNachzahlung 120 Euro");
    }

    [Test]
    public async Task AnUnsupportedFile_LeavesNoText()
    {
        var document = await UploadAsync(TestPdfs.Zip(), "archive.zip", "application/zip");

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Unsupported);
        await Assert.That(await Pipeline.FindContentTextAsync(document.Sha256)).IsNull();
    }

    [Test]
    public async Task AnUnreadableFile_LeavesNoText()
    {
        var document = await UploadAsync(TestPdfs.Truncated(TestPdfs.WithPages(TestPdfs.InvoiceText)));

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Unreadable);
        await Assert.That(await Pipeline.FindContentTextAsync(document.Sha256)).IsNull();
    }

    [Test]
    public async Task TheSameFileUploadedTwice_KeepsOneText()
    {
        var pdf = TestPdfs.WithPages(TestPdfs.InvoiceText);

        var first = await UploadAsync(pdf, "one.pdf");
        var second = await UploadAsync(pdf, "two.pdf");
        await Pipeline.WaitForExtractionAsync(first.Sha256);
        await Pipeline.WaitForExtractionsToSettleAsync();

        await Assert.That(second.Sha256).IsEqualTo(first.Sha256);
        await Assert.That(await Pipeline.CountContentTextsAsync()).IsEqualTo(1);
    }
}
