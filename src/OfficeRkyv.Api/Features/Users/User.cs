namespace OfficeRkyv.Api.Features.Users;

/// <summary>
/// The user aggregate, kept current by an Inline snapshot projection - Fisher folds these Apply
/// methods and writes the document in the same transaction as the event append, so a read
/// immediately after a write is never stale.
/// </summary>
/// <remarks>
/// The Apply dispatcher is source-generated from these method signatures; an event with no matching
/// Apply is a build error, with no reflection and no runtime fallback.
/// </remarks>
public sealed record User : EntityBase
{
    // The address as entered (trimmed), for display.
    [JsonInclude]
    public string Email { get; private set; } = string.Empty;

    // Addresses the stream by email AND claims uniqueness inside the append transaction: a second
    // stream claiming a live key is refused by the store, not by a check-then-write race. Taken from
    // the event, not derived here, so replay can never re-key a stream (see UserCreated).
    [NaturalKey]
    [JsonInclude]
    public string NormalizedEmail { get; private set; } = string.Empty;

    [JsonInclude]
    public string DisplayName { get; private set; } = string.Empty;

    [NaturalKeySource]
    public static User Create(IEvent<UserCreated> @event) =>
        new()
        {
            Id = @event.StreamId,
            Email = @event.Data.Email,
            NormalizedEmail = @event.Data.NormalizedEmail,
            DisplayName = @event.Data.DisplayName,
            Status = EntityStatus.Active,
            CreatedAt = @event.Timestamp,
            UpdatedAt = @event.Timestamp,
        };

    public void Apply(IEvent<UserRenamed> @event)
    {
        DisplayName = @event.Data.DisplayName;
        UpdatedAt = @event.Timestamp;
    }

    public void Apply(IEvent<UserDeleted> @event)
    {
        Status = EntityStatus.Deleted;
        UpdatedAt = @event.Timestamp;
    }
}
