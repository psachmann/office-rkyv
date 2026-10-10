using System.Security.Cryptography;

namespace OfficeRkyv.Api.Shared.Blobs;

/// <summary>
/// <code>
/// &lt;directory&gt;/tmp/&lt;guid&gt;        an upload in progress
/// &lt;directory&gt;/ab/abcdef...    finished content: its SHA-256, in a folder of its first two hex digits
/// </code>
/// <c>tmp</c> is inside the directory so that the final rename never crosses a filesystem and is atomic.
/// </summary>
public sealed class FileSystemBlobStore : IBlobStore
{
    private const string TemporaryFolder = "tmp";
    private const int Sha256HexLength = 64;

    private readonly string directory;
    private readonly string temporaryDirectory;

    public FileSystemBlobStore(string directory)
    {
        this.directory = directory;
        temporaryDirectory = Path.Combine(directory, TemporaryFolder);
        Directory.CreateDirectory(temporaryDirectory);
    }

    public async Task<BlobWriteResult> WriteAsync(
        IAsyncEnumerable<ReadOnlyMemory<byte>> content,
        long maxBytes,
        CancellationToken cancellation = default
    )
    {
        var temporary = Path.Combine(temporaryDirectory, Guid.NewGuid().ToString("n"));

        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long size = 0;

            await using (
                var file = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.Asynchronous
                )
            )
            {
                await foreach (var chunk in content.WithCancellation(cancellation))
                {
                    size += chunk.Length;

                    if (size > maxBytes)
                    {
                        return new BlobWriteResult(BlobWriteStatus.TooLarge);
                    }

                    hash.AppendData(chunk.Span);
                    await file.WriteAsync(chunk, cancellation);
                }
            }

            if (size == 0)
            {
                return new BlobWriteResult(BlobWriteStatus.Empty);
            }

            var sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
            Publish(temporary, PathOf(sha256));

            return new BlobWriteResult(BlobWriteStatus.Stored, sha256, size);
        }
        finally
        {
            // After a successful rename there is nothing here; after anything else this is the cleanup.
            File.Delete(temporary);
        }
    }

    public Stream? OpenRead(string sha256)
    {
        if (!IsSha256(sha256))
        {
            return null;
        }

        try
        {
            return new FileStream(
                PathOf(sha256),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            );
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Deletes what an upload that never finished left in <c>tmp</c>, and returns how many files that was.</summary>
    public int RemoveLeftovers()
    {
        var removed = 0;

        foreach (var file in Directory.EnumerateFiles(temporaryDirectory))
        {
            File.Delete(file);
            removed++;
        }

        return removed;
    }

    private static void Publish(string temporary, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        try
        {
            File.Move(temporary, target, overwrite: false);
        }
        catch (IOException) when (File.Exists(target))
        {
            // The same content is already stored, or another upload of it just got there: same bytes, same name.
        }
    }

    private string PathOf(string sha256) => Path.Combine(directory, sha256[..2], sha256);

    // The hash comes from a recorded fact, not from a client, but it names a path, so it is checked anyway.
    private static bool IsSha256(string value) =>
        value.Length == Sha256HexLength && value.All(character => char.IsAsciiHexDigitLower(character));
}
