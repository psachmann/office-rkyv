namespace OfficeRkyv.Api.Features.Users;

// Payload-only events: Fisher supplies the envelope (stream id, version, timestamp, metadata), so
// there is no per-event wrapper type to write.
public sealed record UserCreated(string Email, string DisplayName);

public sealed record UserRenamed(string DisplayName);

public sealed record UserDeleted;
