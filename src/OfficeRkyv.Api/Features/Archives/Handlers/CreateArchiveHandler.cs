using OfficeRkyv.Api.Features.Users.Handlers;

namespace OfficeRkyv.Api.Features.Archives.Handlers;

public static class CreateArchiveHandler
{
    /// <summary>
    /// A create has no prior state to decide against, so this starts the stream directly. Names are not
    /// unique, so there is nothing to claim. The owner must exist and not be deleted.
    /// </summary>
    public static async Task<Archive> Handle(CreateArchiveCommand command, IDocumentSession session)
    {
        await UserExistence.RequireActiveAsync(session, command.OwnerId);

        var stream = session.Events.StartStream<Archive>(new ArchiveCreated(command.OwnerId, command.Name.Trim()));
        await session.SaveChangesAsync();

        // Inline projection: the snapshot was written in that same transaction.
        return await session.LoadAsync<Archive>(stream.Id)
            ?? throw new InvalidOperationException($"Archive stream '{stream.Id}' produced no snapshot.");
    }
}
