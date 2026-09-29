using Wolverine.Attributes;

namespace OfficeRkyv.Api.Features.Documents.Handlers;

/// <summary>
/// The per-document step of the archive-deletion cascade. It is idempotent and defers to anything newer:
/// it unfiles the document only if it is still live and still in <see cref="ReturnDocumentToInbox.FromArchiveId"/>,
/// and otherwise does nothing.
/// </summary>
public static class ReturnDocumentToInboxHandler
{
    // No expected version: the FromArchiveId check is the guard. A user's write that lands between the
    // fetch and the commit moves the stream, so this commit fails and is retried, and the retry re-reads
    // the document and applies the same check to its new state.
    [RetryNow(typeof(EventStreamUnexpectedMaxEventIdException), 50, 100, 250, 500)]
    public static async Task Handle(ReturnDocumentToInbox message, IDocumentSession session)
    {
        var stream = await session.Events.FetchForWriting<Document>(message.DocumentId);
        var document = stream.Aggregate;

        if (document is { Status: EntityStatus.Active } && document.ArchiveId == message.FromArchiveId)
        {
            stream.AppendOne(new DocumentUnfiled());
        }

        // The Wolverine transaction middleware commits after the handler returns.
    }
}
