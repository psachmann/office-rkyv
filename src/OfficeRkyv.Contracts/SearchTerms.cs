namespace OfficeRkyv.Contracts;

/// <summary>
/// The words a document search query is made of, and its limits. The index matches three-character windows, so
/// a word shorter than that cannot match anything and is ignored rather than turned into an empty result. A
/// client uses the rule to say "type more" before it sends a query; the server applies it too and stays the
/// authority.
/// </summary>
public static class SearchTerms
{
    public const int MinTermLength = 3;

    public const int MaxQueryLength = 200;

    /// <summary>
    /// The query split on whitespace, without the words that have no letter or digit and the words of fewer
    /// than <see cref="MinTermLength"/> characters (counted as Unicode characters, not UTF-16 units).
    /// </summary>
    public static IReadOnlyList<string> From(string query) =>
        [
            .. query
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Any(char.IsLetterOrDigit) && word.EnumerateRunes().Count() >= MinTermLength),
        ];
}
