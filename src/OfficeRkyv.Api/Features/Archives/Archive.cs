namespace OfficeRkyv.Api.Features.Archives;

/// <summary>
/// The archive aggregate: a named collection of one user's documents. Membership is not stored here -
/// each <c>Document</c> records the archive it is in, so "at most one archive" holds by
/// construction, and an archive's contents are a query.
/// </summary>
public sealed record Archive : EntityBase
{
    // Fixed at creation; no event changes it.
    [JsonInclude]
    public Guid OwnerId { get; private set; }

    [JsonInclude]
    public string Name { get; private set; } = string.Empty;

    public static Archive Create(IEvent<ArchiveCreated> @event) =>
        new()
        {
            Id = @event.StreamId,
            OwnerId = @event.Data.OwnerId,
            Name = @event.Data.Name,
            Status = EntityStatus.Active,
            CreatedAt = @event.Timestamp,
            UpdatedAt = @event.Timestamp,
        };

    public void Apply(IEvent<ArchiveRenamed> @event)
    {
        Name = @event.Data.Name;
        UpdatedAt = @event.Timestamp;
    }

    public void Apply(IEvent<ArchiveDeleted> @event)
    {
        Status = EntityStatus.Deleted;
        UpdatedAt = @event.Timestamp;
    }

    // The archive itself did not change, only its version (see ArchiveMembershipChanged), so UpdatedAt stays.
    public void Apply(IEvent<ArchiveMembershipChanged> @event) { }
}
