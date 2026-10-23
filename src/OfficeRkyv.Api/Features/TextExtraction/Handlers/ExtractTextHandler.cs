using OfficeRkyv.Api.Shared.Blobs;
using OfficeRkyv.Api.Shared.Extraction;
using Wolverine.Attributes;

namespace OfficeRkyv.Api.Features.TextExtraction.Handlers;

public static class ExtractTextHandler
{
    /// <summary>
    /// Reads the content once: when an outcome for the hash exists it does nothing, which is what makes a message
    /// that is delivered twice, or a second upload of the same file, harmless. The queue for this message is
    /// sequential, so this is the only writer of outcomes; the natural key on the hash is the store's guarantee
    /// behind that.
    /// </summary>
    /// <remarks>
    /// The text is written before the event, as content is written before the event that records it. A crash in
    /// between leaves a text with no outcome, and the extraction runs again and writes it again. A content that
    /// is missing from the blob store is a fault the extractor has logged; no outcome is recorded and the message
    /// is consumed, so that content that nobody stored (a command made with file facts only) does not fail.
    /// <para>
    /// The work stops as soon as the host begins to stop (<see cref="IHostApplicationLifetime.ApplicationStopping"/>)
    /// and not when the host gives up waiting: Wolverine lets a running handler finish and cancels it only when
    /// the shutdown timeout is over, so closing the app in the middle of a scan would otherwise hang for that long.
    /// The cancelled message stays stored and is handled after the next start.
    /// </para>
    /// <para>
    /// A cancellation is not a failure of the extraction, but Wolverine only knows that for its own token and
    /// moves a message to the error queue when another one cancels it. The retry is scheduled in the stored
    /// message, so it is due when the app has started again.
    /// </para>
    /// </remarks>
    [ScheduleRetry(typeof(OperationCanceledException), 5)]
    public static async Task Handle(
        ExtractText message,
        IDocumentSession session,
        TextExtractor extractor,
        TextStore texts,
        IHostApplicationLifetime lifetime,
        CancellationToken cancellation
    )
    {
        if (
            await FindTextExtractionByHashHandler.Handle(new FindTextExtractionByHashQuery(message.Sha256), session)
            is not null
        )
        {
            return;
        }

        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation,
            lifetime.ApplicationStopping
        );

        if (await extractor.ExtractAsync(message.Sha256, stopping.Token) is not { } result)
        {
            return;
        }

        if (result.Outcome is TextExtractionOutcome.Extracted)
        {
            await texts.WriteAsync(message.Sha256, result.Pages, stopping.Token);

            // The searchable copy commits with the outcome below, so an outcome is never without it.
            session.Store(ContentText.From(message.Sha256, result.Pages));
        }

        session.Events.StartStream<TextExtraction>(
            new TextExtractionCompleted(
                message.Sha256,
                result.Outcome,
                result.PageCount,
                result.OcrPageCount,
                result.UnreadPageCount,
                result.CharacterCount
            )
        );
    }
}
