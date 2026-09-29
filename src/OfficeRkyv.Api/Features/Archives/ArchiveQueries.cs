namespace OfficeRkyv.Api.Features.Archives;

public sealed record FindArchivesByOwnerQuery(Guid OwnerId);

public sealed record FindArchiveByIdQuery(Guid ArchiveId);
