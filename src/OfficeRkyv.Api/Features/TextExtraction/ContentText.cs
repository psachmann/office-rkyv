namespace OfficeRkyv.Api.Features.TextExtraction;

/// <summary>
/// The extracted text of one file content, as the search reads it. There is one per distinct content, keyed by its
/// SHA-256 like the extraction outcome, so two documents with the same file share it.
/// </summary>
/// <remarks>
/// Derived data and not a fact: it is stored as a plain document and not as an aggregate. The text beside the
/// blob (<c>TextStore</c>) is the source, and this row can be deleted and made again from it. It is written in the
/// transaction of the extraction outcome, so the two never disagree.
/// </remarks>
public sealed record ContentText
{
    /// <summary>Lowercase hex SHA-256 of the content: the same key as the extraction outcome and the blob.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The pages in order, the empty ones left out, one after the other on separate lines.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Makes the row for a content from its pages. A page that was not read is an empty page and adds nothing. A
    /// query word has no whitespace, so the line breaks between pages cannot make a match of their own.
    /// </summary>
    public static ContentText From(string sha256, IEnumerable<string> pages) =>
        new() { Id = sha256, Text = string.Join('\n', pages.Where(page => !string.IsNullOrWhiteSpace(page))) };
}
