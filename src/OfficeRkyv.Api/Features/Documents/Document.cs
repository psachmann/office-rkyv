namespace OfficeRkyv.Api.Features.Documents;

/// <summary>
/// One file and its metadata, owned by one user and in at most one archive. The file is fixed at creation and
/// only described here (name, type, size, hash); its bytes are in the blob store. The archive slot lives here,
/// on the document's own stream, so "at most one archive" holds by construction. A document with no archive
/// is in its owner's inbox.
/// </summary>
public sealed record Document : EntityBase
{
    // Fixed at creation; no event changes it.
    [JsonInclude]
    public Guid OwnerId { get; private set; }

    [JsonInclude]
    public string Title { get; private set; } = string.Empty;

    // Null means the document is in the inbox.
    [JsonInclude]
    public Guid? ArchiveId { get; private set; }

    // The file, fixed at creation; no event changes it.
    [JsonInclude]
    public string FileName { get; private set; } = string.Empty;

    [JsonInclude]
    public string ContentType { get; private set; } = string.Empty;

    [JsonInclude]
    public long Size { get; private set; }

    // Lowercase hex SHA-256 of the content: the key of its bytes in the blob store.
    [JsonInclude]
    public string Sha256 { get; private set; } = string.Empty;

    public static Document Create(IEvent<DocumentCreated> @event) =>
        new()
        {
            Id = @event.StreamId,
            OwnerId = @event.Data.OwnerId,
            Title = @event.Data.Title,
            FileName = @event.Data.FileName,
            ContentType = @event.Data.ContentType,
            Size = @event.Data.Size,
            Sha256 = @event.Data.Sha256,
            Status = EntityStatus.Active,
            CreatedAt = @event.Timestamp,
            UpdatedAt = @event.Timestamp,
        };

    public void Apply(IEvent<DocumentRenamed> @event)
    {
        Title = @event.Data.Title;
        UpdatedAt = @event.Timestamp;
    }

    public void Apply(IEvent<DocumentDeleted> @event)
    {
        Status = EntityStatus.Deleted;
        UpdatedAt = @event.Timestamp;
    }

    public void Apply(IEvent<DocumentFiled> @event)
    {
        ArchiveId = @event.Data.ArchiveId;
        UpdatedAt = @event.Timestamp;
    }

    public void Apply(IEvent<DocumentUnfiled> @event)
    {
        ArchiveId = null;
        UpdatedAt = @event.Timestamp;
    }
}
