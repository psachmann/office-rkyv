using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// The scenarios of "Text is extracted after a document is created" and of "A PDF page with a text layer uses it,
/// and a page without one is read by OCR" in the <c>document-text-extraction</c> spec.
/// </summary>
public sealed class TextIsExtractedPipelineTests : ExtractionPipelineTestBase
{
    [Test]
    public async Task AnUpload_IsFollowedByAnExtraction_OfTheTextLayer()
    {
        var pdf = TestPdfs.WithPages(TestPdfs.InvoiceText);

        var document = await UploadAsync(pdf);

        await Assert.That(document.Sha256).IsEqualTo(Sha256Of(pdf));
        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);
        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        await Assert.That(outcome.Sha256).IsEqualTo(document.Sha256);
        var pages = await Pipeline.Texts.ReadPagesAsync(document.Sha256);
        await Assert.That(pages!.Count).IsEqualTo(1);
        await Assert.That(pages[0]).Contains(TestPdfs.InvoiceText);
        await Assert.That(outcome.CharacterCount).IsEqualTo(pages[0].Length);
    }

    [Test]
    public async Task ABornDigitalPdf_NeedsNoOcr()
    {
        var pdf = TestPdfs.WithPages(
            "First page of the contract between the parties",
            "Second page with the terms of payment",
            "Third page holding the signatures"
        );

        var document = await UploadAsync(pdf);

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);
        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        await Assert.That(outcome.PageCount).IsEqualTo(3);
        await Assert.That(outcome.OcrPageCount).IsEqualTo(0);
        await Assert.That(outcome.UnreadPageCount).IsEqualTo(0);
        await Assert.That(Ocr.RenderCalls).IsEqualTo(0);
        await Assert.That(Ocr.ReadCalls).IsEqualTo(0);
        var pages = await Pipeline.Texts.ReadPagesAsync(document.Sha256);
        await Assert.That(pages![1]).Contains("terms of payment");
    }

    [Test]
    public async Task AScannedPdf_IsReadByOcr()
    {
        Ocr.TextOfPage[1] = "Rechnung Nummer 4711";
        Ocr.TextOfPage[2] = "Summe 120 Euro";
        var document = await UploadAsync(TestPdfs.WithoutText(2));

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        await Assert.That(outcome.PageCount).IsEqualTo(2);
        await Assert.That(outcome.OcrPageCount).IsEqualTo(2);
        await Assert.That(outcome.UnreadPageCount).IsEqualTo(0);
        await Assert
            .That(Pipeline.Texts.ReadPagesAsync(document.Sha256).Result)
            .IsEquivalentTo(["Rechnung Nummer 4711", "Summe 120 Euro"]);
        await Assert.That(Ocr.Reads.Select(read => read.Page)).IsEquivalentTo([1, 2]);
    }

    [Test]
    public async Task AMixedPdf_IsDecidedPageByPage()
    {
        Ocr.TextOfPage[2] = "read by ocr";
        var document = await UploadAsync(TestPdfs.WithPages(TestPdfs.InvoiceText, ""));

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.PageCount).IsEqualTo(2);
        await Assert.That(outcome.OcrPageCount).IsEqualTo(1);
        var pages = await Pipeline.Texts.ReadPagesAsync(document.Sha256);
        await Assert.That(pages![0]).Contains(TestPdfs.InvoiceText);
        await Assert.That(pages[1]).IsEqualTo("read by ocr");
        // Only the second page went to OCR.
        await Assert.That(Ocr.Reads.Select(read => read.Page)).IsEquivalentTo([2]);
        await Assert.That(Ocr.RenderCalls).IsEqualTo(1);
    }

    [Test]
    public async Task APageWithOnlyAFewCharacters_IsReadByOcr()
    {
        Ocr.TextOfPage[1] = "the real words of the page";
        var document = await UploadAsync(TestPdfs.WithPages("Page 1"));

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.OcrPageCount).IsEqualTo(1);
        var pages = await Pipeline.Texts.ReadPagesAsync(document.Sha256);
        await Assert.That(pages).IsEquivalentTo(["the real words of the page"]);
    }

    [Test]
    public async Task TheUploadAnswers_WithoutWaitingForTheExtraction()
    {
        Ocr.HoldReads = new TaskCompletionSource();
        var document = await UploadAsync(TestPdfs.WithoutText());

        // The upload has answered while the extraction is still reading its page.
        await Ocr.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(await Pipeline.FindExtractionAsync(document.Sha256)).IsNull();

        Ocr.HoldReads.SetResult();
        await Pipeline.WaitForExtractionAsync(document.Sha256);
    }
}
