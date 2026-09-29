namespace OfficeRkyv.Api.Features.Archives.Handlers;

public static class FindArchivesByOwnerHandler
{
    public static Task<IReadOnlyList<Archive>> Handle(FindArchivesByOwnerQuery query, IQuerySession session) =>
        session
            .Query<Archive>()
            .Where(archive => archive.OwnerId == query.OwnerId && archive.Status != EntityStatus.Deleted)
            .ToListAsync();
}
