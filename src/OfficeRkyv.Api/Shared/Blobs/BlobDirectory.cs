using Microsoft.Data.Sqlite;

namespace OfficeRkyv.Api.Shared.Blobs;

public static class BlobDirectory
{
    /// <summary>
    /// Where the API server keeps file content: the configured directory, or else a folder named after the
    /// database file and beside it. Deriving it from the database ties the two lifetimes together, so a
    /// development database that is new on every run brings new content with it.
    /// </summary>
    public static string For(string connectionString, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var file = new SqliteConnectionStringBuilder(connectionString).DataSource;

        if (string.IsNullOrWhiteSpace(file) || file == ":memory:")
        {
            throw new InvalidOperationException(
                $"File content needs a directory, and the database is not a file. Set {FileStorageOptions.BlobDirectorySetting}."
            );
        }

        return Path.GetFullPath(file) + ".blobs";
    }
}
