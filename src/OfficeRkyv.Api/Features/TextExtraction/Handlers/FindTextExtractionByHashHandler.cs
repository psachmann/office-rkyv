namespace OfficeRkyv.Api.Features.TextExtraction.Handlers;

public static class FindTextExtractionByHashHandler
{
    public static Task<TextExtraction?> Handle(FindTextExtractionByHashQuery query, IQuerySession session) =>
        session.Query<TextExtraction>().Where(extraction => extraction.Sha256 == query.Sha256).FirstOrDefaultAsync()!;
}
