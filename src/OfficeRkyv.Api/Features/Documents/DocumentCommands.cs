namespace OfficeRkyv.Api.Features.Documents;

// DocumentId matches the {AggregateName}Id convention the decider workflow uses to find the stream;
// Version is the expected version, echoed back by the client.
// Created by an upload, after the content has been stored: the file facts describe bytes that are already in
// the blob store under Sha256.
public sealed record CreateDocumentCommand(
    Guid OwnerId,
    string Title,
    string FileName,
    string ContentType,
    long Size,
    string Sha256
);

public sealed record RenameDocumentCommand(Guid DocumentId, int Version, string Title);

public sealed record DeleteDocumentCommand(Guid DocumentId, int Version);

public sealed record FileDocumentCommand(Guid DocumentId, int Version, Guid ArchiveId);

public sealed record UnfileDocumentCommand(Guid DocumentId, int Version);

// Internal cascade message, sent when an archive is deleted; never exposed over gRPC. It carries no
// expected version: FromArchiveId is the guard, so a newer move or delete of the document wins.
public sealed record ReturnDocumentToInbox(Guid DocumentId, Guid FromArchiveId);
