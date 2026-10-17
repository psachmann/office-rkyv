using Fisher.Linq;
using OfficeRkyv.Api.Features.TextExtraction;
using OfficeRkyv.Api.Shared.Extraction;
using OfficeRkyv.Contracts.Documents;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// The scenarios of "The same content is extracted once" and "Extraction does not change the document" in the
/// <c>document-text-extraction</c> spec.
/// </summary>
public sealed class OncePerContentPipelineTests : ExtractionPipelineTestBase
{
    private async Task<int> OutcomeCountAsync()
    {
        await using var session = Pipeline.Store.QuerySession();

        return (await session.Query<TextExtraction>().ToListAsync()).Count;
    }

    [Test]
    public async Task TheSameFileUploadedTwice_HasOneOutcome_AndIsReadOnce()
    {
        var pdf = TestPdfs.WithoutText();
        var first = await UploadAsync(pdf, "first.pdf");
        var second = await UploadAsync(pdf, "second.pdf");

        await Pipeline.WaitForExtractionsToSettleAsync();

        await Assert.That(second.Id).IsNotEqualTo(first.Id);
        await Assert.That(second.Sha256).IsEqualTo(first.Sha256);
        await Assert.That(await OutcomeCountAsync()).IsEqualTo(1);
        await Assert.That(Ocr.ReadCalls).IsEqualTo(1);
        await Assert.That(Ocr.RenderCalls).IsEqualTo(1);
    }

    [Test]
    public async Task TheSameFileUploadedByAnotherUser_IsNotExtractedAgain()
    {
        var bob = await CreateUserAsync("bob@example.com");
        var pdf = TestPdfs.WithoutText();
        var alices = await UploadAsync(pdf, "invoice.pdf");
        await Pipeline.WaitForExtractionAsync(alices.Sha256);

        var bobs = await UploadAsync(pdf, "invoice.pdf", user: bob);
        await Pipeline.WaitForExtractionsToSettleAsync();

        await Assert.That(bobs.Sha256).IsEqualTo(alices.Sha256);
        await Assert.That(await OutcomeCountAsync()).IsEqualTo(1);
        await Assert.That(Ocr.ReadCalls).IsEqualTo(1);
    }

    [Test]
    public async Task TwoDifferentFiles_HaveTheirOwnOutcomes()
    {
        var first = await UploadAsync(TestPdfs.WithPages(TestPdfs.InvoiceText));
        var second = await UploadAsync(TestPdfs.WithPages("A different file with enough letters"));

        await Pipeline.WaitForExtractionAsync(first.Sha256);
        await Pipeline.WaitForExtractionAsync(second.Sha256);

        await Assert.That(await OutcomeCountAsync()).IsEqualTo(2);
    }

    [Test]
    public async Task Extraction_LeavesTheDocumentAsItWas()
    {
        var uploaded = await UploadAsync(TestPdfs.WithPages(TestPdfs.InvoiceText), "invoice.pdf");

        await Pipeline.WaitForExtractionAsync(uploaded.Sha256);

        var listed = (await DocumentsOf(Alice).ListInboxAsync(new ListInboxRequest())).Documents.Single();
        await Assert.That(listed.Version).IsEqualTo(uploaded.Version);
        await Assert.That(listed.Title).IsEqualTo(uploaded.Title);
        await Assert.That(listed.HasArchiveId).IsFalse();
        await Assert.That(listed.FileName).IsEqualTo(uploaded.FileName);
        await Assert.That(listed.ContentType).IsEqualTo(uploaded.ContentType);
        await Assert.That(listed.Size).IsEqualTo(uploaded.Size);
        await Assert.That(listed.Sha256).IsEqualTo(uploaded.Sha256);
        await Assert.That(listed.UpdatedAt).IsEqualTo(uploaded.UpdatedAt);
    }

    [Test]
    public async Task ARenameWithTheUploadsVersion_AfterTheExtraction_Succeeds()
    {
        var uploaded = await UploadAsync(TestPdfs.WithPages(TestPdfs.InvoiceText), "invoice.pdf");
        await Pipeline.WaitForExtractionAsync(uploaded.Sha256);

        var renamed = await DocumentsOf(Alice)
            .RenameDocumentAsync(
                new RenameDocumentRequest
                {
                    DocumentId = uploaded.Id,
                    Version = uploaded.Version,
                    Title = "Invoice March",
                }
            );

        await Assert.That(renamed.Document.Title).IsEqualTo("Invoice March");
    }
}
