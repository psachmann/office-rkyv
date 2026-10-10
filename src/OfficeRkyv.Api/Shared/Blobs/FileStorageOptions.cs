namespace OfficeRkyv.Api.Shared.Blobs;

/// <summary>Bound from the <c>FileStorage</c> configuration section.</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>The key that overrides where the API server keeps file content.</summary>
    public const string BlobDirectorySetting = "FileStorage:BlobDirectory";

    public const long DefaultMaxFileSizeBytes = FileLimits.DefaultMaxFileSizeBytes;

    /// <summary>The largest file an upload may have, in bytes. Larger uploads are refused as they are read.</summary>
    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;
}
