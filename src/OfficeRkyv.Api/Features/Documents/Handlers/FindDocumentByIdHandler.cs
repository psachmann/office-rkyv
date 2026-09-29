namespace OfficeRkyv.Api.Features.Documents.Handlers;

public static class FindDocumentByIdHandler
{
    public static Task<Document?> Handle(FindDocumentByIdQuery query, IQuerySession session) =>
        session.LoadAsync<Document>(query.DocumentId)!;
}
