using OfficeRkyv.Api.Features.Documents.Handlers;
using OfficeRkyv.Api.Shared.Blobs;
using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.Api.Features.TextExtraction;

/// <summary>
/// Makes the kept text of every extracted content searchable when the host starts: for each extraction outcome of
/// <see cref="TextExtractionOutcome.Extracted"/> that has no <see cref="ContentText"/>, it reads the text beside
/// the blob and stores the row. This covers texts extracted before the search existed, and a row that was lost;
/// the text beside the blob stays the source, and no OCR runs.
/// </summary>
/// <remarks>
/// It runs in the background so that the start is not held up by a large archive, and writes in batches so that
/// it is not one long transaction. It is idempotent, and it may run at the same time as an extraction of the same
/// content: both store the same text under the same key, and the store's write is an upsert. A text that is not
/// there is skipped, not an error. It never logs a text, only counts and hashes.
/// </remarks>
internal sealed partial class ContentTextIndexer(
    IDocumentStore store,
    TextStore texts,
    ILogger<ContentTextIndexer> logger
) : BackgroundService
{
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await IndexAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // What was not indexed is found again at the next start; whatever went wrong, the app runs.
            LogIndexingFailed(logger, exception);
        }
    }

    /// <summary>Makes searchable the texts that are not, and logs how many.</summary>
    public async Task IndexAsync(CancellationToken cancellation)
    {
        // The table may not exist yet when this runs before the store's own start-up work; applying the schema
        // twice is harmless.
        await store.ApplyAllConfiguredChangesToDatabaseAsync(cancellation);

        var indexed = 0;
        var missing = 0;

        foreach (var batch in (await MissingAsync(cancellation)).Chunk(BatchSize))
        {
            cancellation.ThrowIfCancellationRequested();
            await using var session = store.LightweightSession();

            foreach (var sha256 in batch)
            {
                if (await texts.ReadPagesAsync(sha256, cancellation) is not { } pages)
                {
                    missing++;
                    LogTextMissing(logger, sha256);

                    continue;
                }

                session.Store(ContentText.From(sha256, pages));
                indexed++;
            }

            await session.SaveChangesAsync(cancellation);
        }

        LogIndexed(logger, indexed, missing);
    }

    // The hashes of the extracted contents that have no text row yet.
    private async Task<List<string>> MissingAsync(CancellationToken cancellation)
    {
        await using var query = store.QuerySession();

        var extractions = await query
            .Query<TextExtraction>()
            .Where(extraction => extraction.Outcome == TextExtractionOutcome.Extracted)
            .ToListAsync(cancellation);
        var present = await query.AdvancedSql.QueryAsync<string>(
            $"select id from {SearchDocumentsHandler.TextTable}",
            cancellation
        );
        var known = present.ToHashSet(StringComparer.Ordinal);

        return [.. extractions.Select(extraction => extraction.Sha256).Where(sha256 => !known.Contains(sha256))];
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Made the text of {Indexed} contents searchable; {Missing} had no kept text"
    )]
    private static partial void LogIndexed(ILogger logger, int indexed, int missing);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The text of content {Sha256} is not kept beside its blob, so it cannot be searched"
    )]
    private static partial void LogTextMissing(ILogger logger, string sha256);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not make the extracted texts searchable")]
    private static partial void LogIndexingFailed(ILogger logger, Exception exception);
}
