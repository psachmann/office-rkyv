using OfficeRkyv.Api.Features.Users.Handlers;

namespace OfficeRkyv.Api.Features.Documents.Handlers;

public static class CreateDocumentHandler
{
    /// <summary>
    /// A create has no prior state to decide against, so this starts the stream directly. The new
    /// document is in no archive, so it lands in the owner's inbox. The owner must exist and not be deleted.
    /// </summary>
    public static async Task<Document> Handle(CreateDocumentCommand command, IDocumentSession session)
    {
        await UserExistence.RequireActiveAsync(session, command.OwnerId);

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

        // Inline projection: the snapshot was written in that same transaction.
        return await session.LoadAsync<Document>(stream.Id)
            ?? throw new InvalidOperationException($"Document stream '{stream.Id}' produced no snapshot.");
    }
}
