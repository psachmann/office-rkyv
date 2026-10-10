namespace OfficeRkyv.Api.Shared.Blobs;

/// <summary>
/// Where file content lives: outside the event store, addressed by the SHA-256 of the content. The same
/// content is stored once, whoever and however often it is written.
/// </summary>
public interface IBlobStore
{
    /// <summary>
    /// Streams the content to storage while hashing and counting it, and keeps it only when it ends complete:
    /// not empty and no larger than <paramref name="maxBytes"/>. Anything else leaves nothing behind.
    /// </summary>
    Task<BlobWriteResult> WriteAsync(
        IAsyncEnumerable<ReadOnlyMemory<byte>> content,
        long maxBytes,
        CancellationToken cancellation = default
    );

    /// <summary>Opens the content for sequential reading, or returns null when this hash is not stored.</summary>
    Stream? OpenRead(string sha256);
}

public enum BlobWriteStatus
{
    Stored,
    Empty,
    TooLarge,
}

/// <param name="Sha256">Lowercase hex; set only when <paramref name="Status"/> is <see cref="BlobWriteStatus.Stored"/>.</param>
/// <param name="Size">The number of bytes stored; zero unless stored.</param>
public sealed record BlobWriteResult(BlobWriteStatus Status, string? Sha256 = null, long Size = 0);
