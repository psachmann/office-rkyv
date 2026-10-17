using OfficeRkyv.Api.Shared.Extraction;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>The scenario of "Waiting extractions survive a restart" in the <c>document-text-extraction</c> spec.</summary>
public sealed class RestartPipelineTests
{
    [Test]
    public async Task AnExtractionThatHadNotCompleted_IsCarriedOut_WhenTheAppStartsAgainOnTheSameData()
    {
        // The first host is in the middle of reading a page when it is stopped. The hosts are disposed in reverse
        // order: the second one deletes the data they share, and the first one only has to be disposed.
        var reading = new FakeOcr { HoldReads = new TaskCompletionSource() };
        await using var first = await PipelineHost.StartAsync(ocr: reading);
        var alice = (
            await new UsersService.UsersServiceClient(first.Channel).CreateUserAsync(
                new CreateUserRequest { Email = "alice@example.com", DisplayName = "Alice" }
            )
        ).User;
        var document = await TestDocuments.UploadAsync(
            new DocumentsService.DocumentsServiceClient(first.ChannelFor(alice.Id)),
            "scan.pdf",
            TestPdfs.WithoutText()
        );
        await reading.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(await first.FindExtractionAsync(document.Sha256)).IsNull();

        await first.StopKeepingDataAsync();

        // Another host on the same database and blob directory, with OCR that answers. The file is not uploaded
        // again.
        var answering = new FakeOcr();
        answering.TextOfPage[1] = "read after the restart";
        await using var second = await PipelineHost.StartAsync(databasePath: first.DatabasePath, ocr: answering);

        var outcome = await second.WaitForExtractionAsync(document.Sha256);
        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        await Assert.That(outcome.OcrPageCount).IsEqualTo(1);
        await Assert
            .That(await second.Texts.ReadPagesAsync(document.Sha256))
            .IsEquivalentTo(["read after the restart"]);
        await Assert.That(answering.ReadCalls).IsEqualTo(1);
    }
}
