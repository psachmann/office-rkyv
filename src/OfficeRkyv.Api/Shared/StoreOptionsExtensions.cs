using Fisher.Storage.FullText;
using OfficeRkyv.Api.Features.Archives;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Api.Features.TextExtraction;
using OfficeRkyv.Api.Features.Users;

namespace OfficeRkyv.Api.Shared;

public static class StoreOptionsExtensions
{
    /// <summary>
    /// Registers the Inline snapshot of every aggregate. One list, shared by the API and the pipeline
    /// tests, so a new aggregate cannot be registered in one and forgotten in the other.
    /// </summary>
    public static void AddAggregateSnapshots(this StoreOptions options)
    {
        options.Projections.Snapshot<User>(SnapshotLifecycle.Inline);
        options.Projections.Snapshot<Archive>(SnapshotLifecycle.Inline);
        options.Projections.Snapshot<Document>(SnapshotLifecycle.Inline);
        options.Projections.Snapshot<TextExtraction>(SnapshotLifecycle.Inline);
    }

    /// <summary>
    /// Declares the full-text indexes. A document's title and file name are searched by what is inside a word
    /// (<c>rechnung</c> in <c>Stromrechnung</c>), which only a trigram index can match. The store keeps the index
    /// current in the transaction that writes the snapshot, and fills it from the documents that already exist.
    /// </summary>
    public static void AddSearchIndexes(this StoreOptions options) =>
        options
            .Schema.For<Document>()
            .FullTextIndex(FullTextTokenizer.Trigram, document => document.Title, document => document.FileName);
}
