namespace OfficeRkyv.Api.Features.Archives;

// ArchiveId matches the {AggregateName}Id convention the decider workflow uses to find the stream;
// Version is the expected version, echoed back by the client.
// OwnerId is the caller, filled in by the service. CallerId is the user acting on an existing archive: the
// existence check refuses (as not found) an archive that is not theirs.
public sealed record CreateArchiveCommand(Guid OwnerId, string Name);

public sealed record RenameArchiveCommand(Guid ArchiveId, int Version, string Name, Guid CallerId);

public sealed record DeleteArchiveCommand(Guid ArchiveId, int Version, Guid CallerId);
