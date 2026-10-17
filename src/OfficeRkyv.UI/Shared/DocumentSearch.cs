using OfficeRkyv.Contracts.Documents;

namespace OfficeRkyv.UI.Shared;

/// <summary>What a search is asked: whose documents, and the words to find in their titles and file names.</summary>
public sealed record DocumentSearch(Guid OwnerId, string Query);

/// <summary>The best matches, and whether more documents matched than the server returns.</summary>
public sealed record DocumentSearchResult(IReadOnlyList<DocumentDto> Documents, bool HasMore);
