using FluentValidation;
using OfficeRkyv.Api.Features.TextExtraction;

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
/// Finds an owner's live documents in which every word of the query is found: in the title, in the file name, or
/// in the text extracted from the document's file. Documents whose title and file name hold every word come
/// first, best match first; documents that need their text for some word follow, by title.
/// </summary>
/// <remarks>
/// Raw SQL against the full-text indexes Fisher keeps for <see cref="Document"/> and for <see cref="ContentText"/>
/// (see <c>StoreOptionsExtensions.AddSearchIndexes</c>), because Fisher's LINQ operators cannot rank a query that
/// has more than one search term. Two statements, because the first is ranked by <c>bm25()</c> of its index and
/// the second has no score that compares to it, and because the second does not run when the first fills the
/// page. The names below are Fisher's own storage names, which it does not promise: the pipeline tests of every
/// search scenario run these queries, so a change shows up there.
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
    private const string Sha256Member = "$.sha256";

    // The same for the extracted text of a content, which is keyed by the hash of the content.
    internal const string TextTable = "fi_doc_contenttext";
    private const string TextIndexTable = "fi_fts_contenttext";

    public static async Task<SearchDocumentsResult> Handle(
        SearchDocumentsQuery query,
        IQuerySession session,
        CancellationToken cancellation
    )
    {
        var fields = string.Join(", ", session.AdvancedSql.SelectFieldsFor<Document>().Select(field => $"d.{field}"));
        var terms = SearchTerms.From(query.Query);
        var owner = query.OwnerId.ToString();
        var allWords = MatchAll(terms);

        // One more than the limit, to know whether the limit cut anything off.
        var documents = (
            await session.AdvancedSql.QueryAsync<Document>(
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
                allWords,
                owner,
                (int)EntityStatus.Deleted
            )
        ).ToList();

        if (documents.Count <= Limit)
        {
            documents.AddRange(
                await FindThroughText(
                    session,
                    fields,
                    terms,
                    allWords,
                    owner,
                    Limit + 1 - documents.Count,
                    cancellation
                )
            );
        }

        return new SearchDocumentsResult([.. documents.Take(Limit)], documents.Count > Limit);
    }

    // The documents the title query did not return, in which each word is in the title or file name, or in the
    // text of the document's file. The text index answers once per word, as a set of content hashes.
    private static async Task<IReadOnlyList<Document>> FindThroughText(
        IQuerySession session,
        string fields,
        IReadOnlyList<string> terms,
        string allWords,
        string owner,
        int take,
        CancellationToken cancellation
    )
    {
        var eachWord = string.Join(
            " and ",
            terms.Select(_ =>
                $"""
                    (d.rowid in (select rowid from {IndexTable} where {IndexTable} match ?)
                      or json_extract(d.data, '{Sha256Member}') in (
                        select c.id from {TextIndexTable} t join {TextTable} c on c.rowid = t.rowid where {TextIndexTable} match ?))
                    """
            )
        );

        // Placeholders in the order of the statement: the owner, deletion, the title query's own match, and a
        // word twice (title index, text index) for each word.
        object[] arguments =
        [
            owner,
            (int)EntityStatus.Deleted,
            allWords,
            .. terms.SelectMany(term => new object[] { Quote(term), Quote(term) }),
        ];

        return await session.AdvancedSql.QueryAsync<Document>(
            $"""
            select {fields}
            from {DocumentTable} d
            where json_extract(d.data, '{OwnerMember}') = ?
              and json_extract(d.data, '{StatusMember}') <> ?
              and d.rowid not in (select rowid from {IndexTable} where {IndexTable} match ?)
              and {eachWord}
            order by json_extract(d.data, '{TitleMember}')
            limit {take}
            """,
            cancellation,
            arguments
        );
    }

    // Each word is one quoted FTS5 string, so a word can never be read as search syntax (a quote inside it is
    // doubled), and the strings are ANDed.
    private static string MatchAll(IEnumerable<string> terms) => string.Join(" AND ", terms.Select(Quote));

    private static string Quote(string term) => $"\"{term.Replace("\"", "\"\"")}\"";
}
