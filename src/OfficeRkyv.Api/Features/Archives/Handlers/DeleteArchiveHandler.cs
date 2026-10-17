using Wolverine;
using Wolverine.Fisher;

namespace OfficeRkyv.Api.Features.Archives.Handlers;

public static class DeleteArchiveHandler
{
    public static Task ValidateAsync(DeleteArchiveCommand command, IQuerySession session) =>
        ArchiveExistence.RequireAsync(session, command.ArchiveId, command.CallerId);

    /// <summary>
    /// Deletes the archive and cascades: one <see cref="ReturnDocumentToInbox"/> per member document. Wolverine
    /// persists those messages in its outbox inside the same transaction as <see cref="ArchiveDeleted"/>, so a
    /// commit cannot lose them, and delivers them locally afterwards - so the documents are back in the inbox
    /// shortly after the deletion is acknowledged, not as part of it.
    /// </summary>
    /// <remarks>
    /// The expected version guard is what closes the race with a concurrent filing (design D6): every filing
    /// moves the archive's version, so a filing committed after the archive was loaded fails this commit with a
    /// concurrency conflict instead of leaving a document this query never saw.
    /// </remarks>
    [DeciderFunction]
    public static async Task<(Events, OutgoingMessages)> Handle(
        DeleteArchiveCommand command,
        Archive archive,
        IQuerySession session
    )
    {
        var events = new Events();
        var messages = new OutgoingMessages();

        // Idempotent: deleting a deleted archive succeeds and changes nothing.
        if (archive.Status is EntityStatus.Deleted)
        {
            return (events, messages);
        }

        var members = await session
            .Query<Document>()
            .Where(document => document.ArchiveId == archive.Id && document.Status != EntityStatus.Deleted)
            .ToListAsync();

        events.Add(new ArchiveDeleted());
        messages.AddRange(members.Select(member => new ReturnDocumentToInbox(member.Id, archive.Id)));

        return (events, messages);
    }
}
