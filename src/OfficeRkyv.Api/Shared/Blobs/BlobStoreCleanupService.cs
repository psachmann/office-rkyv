namespace OfficeRkyv.Api.Shared.Blobs;

/// <summary>
/// Clears what an upload killed by a crash left in the blob store's <c>tmp</c> folder, once when the host
/// starts. Nothing is writing then, so everything in there is a leftover.
/// </summary>
internal sealed partial class BlobStoreCleanupService(
    FileSystemBlobStore store,
    ILogger<BlobStoreCleanupService> logger
) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var removed = store.RemoveLeftovers();

        if (removed > 0)
        {
            LogLeftoversRemoved(logger, removed);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Removed {Count} unfinished upload file(s) left in the blob store's tmp folder"
    )]
    private static partial void LogLeftoversRemoved(ILogger logger, int count);
}
