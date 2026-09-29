namespace OfficeRkyv.Api.Features.Documents;

/// <summary>
/// A document's metadata (no file content yet), owned by one user and in at most one archive. The archive
/// slot lives here, on the document's own stream, so "at most one archive" holds by construction. A
/// document with no archive is in its owner's inbox.
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

    public static Document Create(IEvent<DocumentCreated> @event) =>
        new()
        {
            Id = @event.StreamId,
            OwnerId = @event.Data.OwnerId,
            Title = @event.Data.Title,
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
