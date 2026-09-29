namespace OfficeRkyv.Api.Features.Archives.Handlers;

public static class FindArchiveByIdHandler
{
    public static Task<Archive?> Handle(FindArchiveByIdQuery query, IQuerySession session) =>
        session.LoadAsync<Archive>(query.ArchiveId)!;
}
