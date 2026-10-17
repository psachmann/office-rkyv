namespace OfficeRkyv.Api.Features.Documents;

// DocumentId matches the {AggregateName}Id convention the decider workflow uses to find the stream;
// Version is the expected version, echoed back by the client. CallerId is the user acting on an existing document:
// the existence check refuses (as not found) a document that is not theirs.

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

public sealed record RenameDocumentCommand(Guid DocumentId, int Version, string Title, Guid CallerId);

public sealed record DeleteDocumentCommand(Guid DocumentId, int Version, Guid CallerId);

public sealed record FileDocumentCommand(Guid DocumentId, int Version, Guid ArchiveId, Guid CallerId);

public sealed record UnfileDocumentCommand(Guid DocumentId, int Version, Guid CallerId);

// Internal cascade message, sent when an archive is deleted; never exposed over gRPC. It carries no
// expected version: FromArchiveId is the guard, so a newer move or delete of the document wins.
public sealed record ReturnDocumentToInbox(Guid DocumentId, Guid FromArchiveId);
