using Microsoft.Extensions.DependencyInjection;
using OfficeRkyv.Api.Shared.Extraction;
using OfficeRkyv.Contracts.Documents;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// The scenarios of "A page that OCR cannot read is counted, not fatal" and "The text is kept page by page" in the
/// <c>document-text-extraction</c> spec.
/// </summary>
public sealed class UnreadPagesPipelineTests : ExtractionPipelineTestBase
{
    [Test]
    public async Task WithoutOcr_ThePagesWithATextLayerAreKept_AndTheOthersAreCountedAsUnread()
    {
        Ocr.Unavailable = true;
        var document = await UploadAsync(TestPdfs.WithPages(TestPdfs.InvoiceText, ""));

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        await Assert.That(outcome.PageCount).IsEqualTo(2);
        await Assert.That(outcome.OcrPageCount).IsEqualTo(0);
        await Assert.That(outcome.UnreadPageCount).IsEqualTo(1);
        var pages = await Pipeline.Texts.ReadPagesAsync(document.Sha256);
        await Assert.That(pages!.Count).IsEqualTo(2);
        await Assert.That(pages[0]).Contains(TestPdfs.InvoiceText);
        await Assert.That(pages[1]).IsEmpty();
    }

    [Test]
    public async Task WhenOcrFailsOnOnePage_TheOthersAreStillRead()
    {
        Ocr.FailOnPages.Add(2);
        Ocr.TextOfPage[1] = "page one";
        Ocr.TextOfPage[3] = "page three";
        var document = await UploadAsync(TestPdfs.WithoutText(3));

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        await Assert.That(outcome.PageCount).IsEqualTo(3);
        await Assert.That(outcome.OcrPageCount).IsEqualTo(2);
        await Assert.That(outcome.UnreadPageCount).IsEqualTo(1);
        await Assert
            .That(await Pipeline.Texts.ReadPagesAsync(document.Sha256))
            .IsEquivalentTo(["page one", "", "page three"]);
    }

    [Test]
    public async Task AnImageWithoutOcr_IsOnePageThatWasNotRead_AndKeepsEmptyText()
    {
        Ocr.Unavailable = true;
        var document = await UploadAsync(TestPdfs.Jpeg(), "photo.jpg", "image/jpeg");

        var outcome = await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        await Assert.That(outcome.PageCount).IsEqualTo(1);
        await Assert.That(outcome.OcrPageCount).IsEqualTo(0);
        await Assert.That(outcome.UnreadPageCount).IsEqualTo(1);
        await Assert.That(outcome.CharacterCount).IsEqualTo(0);
        await Assert.That(await Pipeline.Texts.ReadPagesAsync(document.Sha256)).IsEquivalentTo([""]);
    }

    [Test]
    public async Task APageThatWasNotRead_KeepsItsPosition_InTheKeptText()
    {
        Ocr.FailOnPages.Add(2);
        Ocr.TextOfPage[1] = "first";
        Ocr.TextOfPage[3] = "third";
        var document = await UploadAsync(TestPdfs.WithoutText(3));
        await Pipeline.WaitForExtractionAsync(document.Sha256);

        var pages = await Pipeline.Texts.ReadPagesAsync(document.Sha256);

        await Assert.That(pages!.Count).IsEqualTo(3);
        await Assert.That(pages[0]).Contains("first");
        await Assert.That(pages[1]).IsEmpty();
        await Assert.That(pages[2]).Contains("third");
    }

    [Test]
    public async Task WithTheRealProgramsMissing_TheAppStartsAndUploadsStillWork_AndTheScannedPageIsCountedAsUnread()
    {
        // The real runner, pointed at programs that are not there: nothing is faked below the API.
        await using var host = await PipelineHost.StartAsync(configure: builder =>
            builder.Services.Configure<TextExtractionOptions>(options =>
            {
                options.TesseractPath = "no-such-tesseract-office-rkyv";
                options.PdftoppmPath = "no-such-pdftoppm-office-rkyv";
            })
        );
        var alice = (
            await new Contracts.Users.UsersService.UsersServiceClient(host.Channel).CreateUserAsync(
                new Contracts.Users.CreateUserRequest { Email = "alice@example.com", DisplayName = "Alice" }
            )
        ).User;
        var client = new DocumentsService.DocumentsServiceClient(host.ChannelFor(alice.Id));
        var pdf = TestPdfs.WithPages(TestPdfs.InvoiceText, "");

        var document = await TestDocuments.UploadAsync(client, "mixed.pdf", pdf);

        var outcome = await host.WaitForExtractionAsync(document.Sha256);
        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        await Assert.That(outcome.PageCount).IsEqualTo(2);
        await Assert.That(outcome.UnreadPageCount).IsEqualTo(1);
        await Assert.That(await TestDocuments.DownloadAsync(client, document.Id)).IsEquivalentTo(pdf);
    }
}
