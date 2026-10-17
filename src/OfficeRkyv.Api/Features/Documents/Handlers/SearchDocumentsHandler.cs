using FluentValidation;

namespace OfficeRkyv.Api.Features.Documents.Handlers;

/// <summary>
/// The input rules of a search: not longer than <see cref="MaxQueryLength"/>, and at least one word that the
/// index can match (see <see cref="SearchTerms"/>). Public and dependency-free, so Wolverine's FluentValidation
/// discovery finds it.
/// </summary>
public sealed class SearchDocumentsValidator : AbstractValidator<SearchDocumentsQuery>
{
    public const int MaxQueryLength = SearchTerms.MaxQueryLength;

    public SearchDocumentsValidator()
    {
        RuleFor(q => q.Query.Trim())
            .MaximumLength(MaxQueryLength)
            .WithErrorCode(ViolationCodes.TooLong)
            .OverridePropertyName(nameof(SearchDocumentsQuery.Query));

        // An empty query is too short and not "required": the client has one rule to explain.
        RuleFor(q => q.Query).Must(query => SearchTerms.From(query).Count > 0).WithErrorCode(ViolationCodes.TooShort);
    }
}

/// <summary>
/// Finds an owner's live documents whose title or file name contains every word of the query, best match first.
/// </summary>
/// <remarks>
/// Raw SQL against the full-text index Fisher keeps for <see cref="Document"/> (see
/// <c>StoreOptionsExtensions.AddSearchIndexes</c>), because Fisher's LINQ operators cannot rank a query that
/// has more than one search term. The names below are Fisher's own storage names, which it does not promise:
/// the pipeline tests of every search scenario run this query, so a change shows up there.
/// </remarks>
public static class SearchDocumentsHandler
{
    public const int Limit = 50;

    // Fisher's names for the document table, its full-text table, and the JSON members of a stored Document.
    private const string DocumentTable = "fi_doc_document";
    private const string IndexTable = "fi_fts_document";
    private const string OwnerMember = "$.ownerId";
    private const string StatusMember = "$.status";
    private const string TitleMember = "$.title";

    public static async Task<SearchDocumentsResult> Handle(
        SearchDocumentsQuery query,
        IQuerySession session,
        CancellationToken cancellation
    )
    {
        var fields = string.Join(", ", session.AdvancedSql.SelectFieldsFor<Document>().Select(field => $"d.{field}"));

        // One more than the limit, to know whether the limit cut anything off.
        var documents = await session.AdvancedSql.QueryAsync<Document>(
            $"""
            select {fields}
            from {IndexTable} f
            join {DocumentTable} d on d.rowid = f.rowid
            where {IndexTable} match ?
              and json_extract(d.data, '{OwnerMember}') = ?
              and json_extract(d.data, '{StatusMember}') <> ?
            order by bm25({IndexTable}), json_extract(d.data, '{TitleMember}')
            limit {Limit + 1}
            """,
            cancellation,
            MatchAll(SearchTerms.From(query.Query)),
            query.OwnerId.ToString(),
            (int)EntityStatus.Deleted
        );

        return new SearchDocumentsResult([.. documents.Take(Limit)], documents.Count > Limit);
    }

    // Each word is one quoted FTS5 string, so a word can never be read as search syntax (a quote inside it is
    // doubled), and the strings are ANDed.
    private static string MatchAll(IEnumerable<string> terms) =>
        string.Join(" AND ", terms.Select(term => $"\"{term.Replace("\"", "\"\"")}\""));
}
