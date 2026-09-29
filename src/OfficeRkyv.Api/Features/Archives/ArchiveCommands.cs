namespace OfficeRkyv.Api.Features.Archives;

// ArchiveId matches the {AggregateName}Id convention the decider workflow uses to find the stream;
// Version is the expected version, echoed back by the client.
public sealed record CreateArchiveCommand(Guid OwnerId, string Name);

public sealed record RenameArchiveCommand(Guid ArchiveId, int Version, string Name);

public sealed record DeleteArchiveCommand(Guid ArchiveId, int Version);
