namespace OfficeRkyv.Contracts;

/// <summary>
/// What a client and the server must agree on about file transfer. A client uses the limit to refuse a file
/// before it sends it; the server's own setting (<c>FileStorage:MaxFileSizeBytes</c>) stays the authority.
/// </summary>
public static class FileLimits
{
    /// <summary>The largest file an upload may have unless the server is configured otherwise: 100 MB.</summary>
    public const long DefaultMaxFileSizeBytes = 100L * 1024 * 1024;

    /// <summary>
    /// The size of one chunk of file content on the wire: far below gRPC's 4 MB message limit, so no limit has
    /// to be raised.
    /// </summary>
    public const int ChunkSize = 64 * 1024;
}
