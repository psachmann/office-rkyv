using JasperFx.Events.Aggregation;

namespace OfficeRkyv.Api.Features.Users;

/// <summary>
/// The user aggregate, kept current by an Inline snapshot projection - Fisher folds these Apply
/// methods and writes the resulting document in the same transaction as the event append, so a read
/// immediately after a write is never stale.
/// </summary>
/// <remarks>
/// The Apply dispatcher is source-generated from these method signatures; an event with no matching
/// Apply is a build error, with no reflection and no runtime fallback.
/// </remarks>
public sealed class UserEntity
{
    public Guid Id { get; set; }

    // Addresses the stream by email AND claims uniqueness inside the append transaction: a second
    // stream claiming a live key is refused by the store, not by a check-then-write race in a handler.
    [NaturalKey]
    public string Email { get; set; } = "";

    public string DisplayName { get; set; } = "";

    // Soft delete: the document stays queryable and callers filter explicitly.
    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [NaturalKeySource]
    public static UserEntity Create(JasperFx.Events.IEvent<UserCreated> @event) =>
        new()
        {
            Id = @event.StreamId,
            Email = @event.Data.Email,
            DisplayName = @event.Data.DisplayName,
            CreatedAt = @event.Timestamp,
            UpdatedAt = @event.Timestamp,
        };

    public void Apply(JasperFx.Events.IEvent<UserRenamed> @event)
    {
        DisplayName = @event.Data.DisplayName;
        UpdatedAt = @event.Timestamp;
    }

    public void Apply(JasperFx.Events.IEvent<UserDeleted> @event)
    {
        IsDeleted = true;
        UpdatedAt = @event.Timestamp;
    }
}
