namespace OfficeRkyv.Api.Features.Users;

// Payload-only events: Fisher supplies the envelope (stream id, version, timestamp, metadata), so
// there is no per-event wrapper type to write.

// NormalizedEmail is recorded, not derived on replay: it is the value that decided uniqueness, so it
// belongs to the history. A later change to the normalization rule then cannot silently re-key users.
public sealed record UserCreated(string Email, string NormalizedEmail, string DisplayName);

public sealed record UserRenamed(string DisplayName);

public sealed record UserDeleted;
