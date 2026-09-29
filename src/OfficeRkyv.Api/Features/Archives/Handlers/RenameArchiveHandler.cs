namespace OfficeRkyv.Api.Features.Archives.Handlers;

public static class RenameArchiveHandler
{
    public static Task ValidateAsync(RenameArchiveCommand command, IQuerySession session) =>
        ArchiveExistence.RequireAsync(session, command.ArchiveId);

    [DeciderFunction]
    public static IEnumerable<object> Handle(RenameArchiveCommand command, Archive archive)
    {
        if (archive.Status is EntityStatus.Deleted)
        {
            throw new InvariantViolationException(InvariantCodes.ArchiveDeleted);
        }

        var name = command.Name.Trim();

        if (archive.Name != name)
        {
            yield return new ArchiveRenamed(name);
        }
    }
}
