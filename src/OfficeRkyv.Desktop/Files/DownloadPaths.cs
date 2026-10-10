namespace OfficeRkyv.Desktop.Files;

/// <summary>Where, and under what name, a downloaded file lands when the desktop saves it without asking.</summary>
internal static class DownloadPaths
{
    /// <summary>
    /// A name that is only a name: whatever precedes the last path separator is dropped, so a file called
    /// <c>../../x</c> cannot leave the directory it is saved in.
    /// </summary>
    public static string SafeFileName(string fileName)
    {
        var name = fileName[(fileName.LastIndexOfAny(['/', '\\']) + 1)..].Trim();

        return name.Length == 0 || name is "." or ".." ? "download" : name;
    }

    /// <summary>
    /// <paramref name="fileName"/> in <paramref name="directory"/>, or when that is taken, the first of
    /// <c>name (1).ext</c>, <c>name (2).ext</c>, ... that is free. An existing file is never overwritten.
    /// </summary>
    /// <param name="exists">Whether a path is taken; the file system's <see cref="File.Exists"/> in the app.</param>
    public static string Unique(string directory, string fileName, Func<string, bool> exists)
    {
        var safe = SafeFileName(fileName);
        var candidate = Path.Combine(directory, safe);

        if (!exists(candidate))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(safe);
        var extension = Path.GetExtension(safe);

        for (var number = 1; ; number++)
        {
            candidate = Path.Combine(directory, $"{stem} ({number}){extension}");

            if (!exists(candidate))
            {
                return candidate;
            }
        }
    }
}
