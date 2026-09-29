namespace OfficeRkyv.Api.Features.Archives;

// Payload-only events: Fisher supplies the envelope (stream id, version, timestamp, metadata).
public sealed record ArchiveCreated(Guid OwnerId, string Name);

public sealed record ArchiveRenamed(string Name);

public sealed record ArchiveDeleted;

// Appended on every filing into the archive. It carries no data: its only job is to move the archive
// stream's version, so that a delete racing with a filing loses on the ordinary expected-version check
// instead of missing the late-filed document.
public sealed record ArchiveMembershipChanged;
