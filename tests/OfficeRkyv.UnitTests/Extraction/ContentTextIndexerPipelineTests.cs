using Fisher;
using Microsoft.Extensions.Logging;
using OfficeRkyv.Api.Features.TextExtraction;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// The scenarios of "Texts extracted before the search existed are made searchable at start" in the
/// <c>document-content-index</c> spec. Data from before the search is a database that holds an extraction outcome
/// and its kept text but no searchable text, which is what a host stopped and a deleted row leave.
/// </summary>
public sealed class ContentTextIndexerPipelineTests
{
    private const string Contract = "Kaufvertrag fuer das Haus";

    // A host that extracted one document and was stopped, with its searchable text removed as if it never was.
    private static async Task<(PipelineHost First, UserDto Alice, DocumentDto Document)> DataFromBeforeAsync()
    {
        var first = await PipelineHost.StartAsync(ocr: new FakeOcr());
        var alice = (
            await new UsersService.UsersServiceClient(first.Channel).CreateUserAsync(
                new CreateUserRequest { Email = "alice@example.com", DisplayName = "Alice" }
            )
        ).User;
        var document = await TestDocuments.UploadAsync(
            new DocumentsService.DocumentsServiceClient(first.ChannelFor(alice.Id)),
            "scan.pdf",
            TestPdfs.WithPages(Contract),
            title: "Scan"
        );
        await first.WaitForExtractionAsync(document.Sha256);
        await first.StopKeepingDataAsync();
        await DeleteContentTextsAsync(first.DatabasePath);

        return (first, alice, document);
    }

    // A store configured as the hosts are, over the database file of a host that is stopped.
    private static async Task DeleteContentTextsAsync(string databasePath)
    {
        using var store = DocumentStore.For(options =>
        {
            options.Connection($"Data Source={databasePath}");
            options.AddAggregateSnapshots();
            options.AddSearchIndexes();
        });
        await store.ApplyAllConfiguredChangesToDatabaseAsync();
        await using var session = store.LightweightSession();
        session.DeleteWhere<ContentText>(text => text.Id != "");
        await session.SaveChangesAsync();
    }

    private static async Task<string[]> TitlesAsync(PipelineHost host, UserDto user, string query) =>
        [
            .. (
                await new DocumentsService.DocumentsServiceClient(host.ChannelFor(user.Id)).SearchDocumentsAsync(
                    new SearchDocumentsRequest { Query = query }
                )
            ).Documents.Select(document => document.Title),
        ];

    [Test]
    public async Task AnExtractedText_WithNoSearchableText_IsMadeSearchable_WithoutOcr()
    {
        var (first, alice, document) = await DataFromBeforeAsync();
        await using var _ = first;
        var ocr = new FakeOcr();

        await using var second = await PipelineHost.StartAsync(databasePath: first.DatabasePath, ocr: ocr);
        await second.WaitForIndexingAsync();

        await Assert.That(await second.FindContentTextAsync(document.Sha256)).IsNotNull();
        await Assert.That(await TitlesAsync(second, alice, "kaufvertrag")).IsEquivalentTo(["Scan"]);
        await Assert.That(ocr.RenderCalls).IsEqualTo(0);
        await Assert.That(ocr.ReadCalls).IsEqualTo(0);
    }

    [Test]
    public async Task StartingAgain_ChangesNothing()
    {
        var (first, alice, document) = await DataFromBeforeAsync();
        await using var _ = first;

        await using var second = await PipelineHost.StartAsync(databasePath: first.DatabasePath, ocr: new FakeOcr());
        await second.WaitForIndexingAsync();
        await second.StopKeepingDataAsync();
        await using var third = await PipelineHost.StartAsync(databasePath: first.DatabasePath, ocr: new FakeOcr());
        await third.WaitForIndexingAsync();

        await Assert.That(await third.CountContentTextsAsync()).IsEqualTo(1);
        await Assert.That(await TitlesAsync(third, alice, "kaufvertrag")).IsEquivalentTo(["Scan"]);
    }

    [Test]
    public async Task AnOutcome_WhoseTextIsNotThere_LetsTheAppStart_AndTheDocumentBeFoundByItsTitle()
    {
        var (first, alice, document) = await DataFromBeforeAsync();
        await using var _ = first;
        File.Delete(Path.Combine(first.BlobDirectory, document.Sha256[..2], document.Sha256 + ".txt"));

        await using var second = await PipelineHost.StartAsync(databasePath: first.DatabasePath, ocr: new FakeOcr());
        await second.WaitForIndexingAsync();

        await Assert.That(await second.FindContentTextAsync(document.Sha256)).IsNull();
        await Assert.That(await TitlesAsync(second, alice, "scan")).IsEquivalentTo(["Scan"]);
    }

    [Test]
    public async Task TheIndexer_LogsCountsAndHashes_NeverAText()
    {
        var (first, _, document) = await DataFromBeforeAsync();
        await using var _ = first;
        var logger = new CapturingLogger<ContentTextIndexer>();
        await using var second = await PipelineHost.StartAsync(databasePath: first.DatabasePath, ocr: new FakeOcr());
        await second.WaitForIndexingAsync();
        await DeleteContentTextsAsyncOn(second);
        File.Delete(Path.Combine(first.BlobDirectory, document.Sha256[..2], document.Sha256 + ".txt"));

        await new ContentTextIndexer(second.Store, second.Texts, logger).IndexAsync(CancellationToken.None);

        await Assert.That(logger.Entries.Count).IsEqualTo(2);
        await Assert
            .That(logger.Entries)
            .Contains(entry => entry.Level == LogLevel.Warning && entry.Message.Contains(document.Sha256));
        await Assert
            .That(logger.Entries)
            .Contains(entry =>
                entry.Level == LogLevel.Information
                && entry.Message.Contains("0 contents")
                && entry.Message.Contains("1 had")
            );
        await Assert.That(logger.Entries.Any(entry => entry.Message.Contains("Kaufvertrag"))).IsFalse();
    }

    private static async Task DeleteContentTextsAsyncOn(PipelineHost host)
    {
        await using var session = host.Store.LightweightSession();
        session.DeleteWhere<ContentText>(text => text.Id != "");
        await session.SaveChangesAsync();
    }
}
