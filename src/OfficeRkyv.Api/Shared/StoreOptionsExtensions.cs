using OfficeRkyv.Api.Features.Archives;
using OfficeRkyv.Api.Features.Documents;
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
    }
}
