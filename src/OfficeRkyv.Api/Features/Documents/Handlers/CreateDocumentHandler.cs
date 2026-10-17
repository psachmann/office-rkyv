using OfficeRkyv.Api.Features.TextExtraction;
using OfficeRkyv.Api.Features.Users.Handlers;
using Wolverine.Attributes;
using Wolverine.Fisher;
using Wolverine.Runtime;

namespace OfficeRkyv.Api.Features.Documents.Handlers;

public static class CreateDocumentHandler
{
    /// <summary>
    /// A create has no prior state to decide against, so this starts the stream directly. The new
    /// document is in no archive, so it lands in the owner's inbox. The owner must exist and not be deleted.
    /// </summary>
    /// <remarks>
    /// The text of the file is extracted afterwards, in the background (<see cref="ExtractText"/>). The message
    /// is stored in the transaction that appends <see cref="DocumentCreated"/>, so a committed document always
    /// has its message and a failed one has none. That needs the handler to enroll the outbox in the session
    /// itself and to stay out of Wolverine's automatic transaction: a publish on the injected bus together with
    /// this handler's own save is stored twice by that transaction and fails (design D1, spike 1.1).
    /// </remarks>
    [NonTransactional]
    public static async Task<Document> Handle(
        CreateDocumentCommand command,
        IDocumentSession session,
        IFisherOutbox outbox
    )
    {
        await UserExistence.RequireActiveAsync(session, command.OwnerId);

        outbox.Enroll(session);
        await outbox.PublishAsync(new ExtractText(command.Sha256));

        var stream = session.Events.StartStream<Document>(
            new DocumentCreated(
                command.OwnerId,
                command.Title.Trim(),
                command.FileName.Trim(),
                command.ContentType.Trim(),
                command.Size,
                command.Sha256
            )
        );
        await session.SaveChangesAsync();

        // The message is stored by now; this hands it to the queue at once. Without it, it would wait for the
        // next start of the app. IFisherOutbox does not offer this, but what it hands out is a MessageContext.
        await ((MessageContext)outbox).FlushOutgoingMessagesAsync();

        // Inline projection: the snapshot was written in that same transaction.
        return await session.LoadAsync<Document>(stream.Id)
            ?? throw new InvalidOperationException($"Document stream '{stream.Id}' produced no snapshot.");
    }
}
