namespace OfficeRkyv.Api.Features.Documents;

// Payload-only events: Fisher supplies the envelope (stream id, version, timestamp, metadata).

// The file is part of the creation fact and no later event changes it, so a document always has exactly one
// file. Only facts about the content are recorded; the bytes live in the blob store under Sha256.
public sealed record DocumentCreated(
    Guid OwnerId,
    string Title,
    string FileName,
    string ContentType,
    long Size,
    string Sha256
);

public sealed record DocumentRenamed(string Title);

public sealed record DocumentDeleted;

// Moves from the inbox or from another archive. There is no "removed from the old archive" event: the
// document holds one slot, so filing replaces whatever was there.
public sealed record DocumentFiled(Guid ArchiveId);

// No payload: the previous archive is in the stream's history and the snapshot does not need it.
public sealed record DocumentUnfiled;
