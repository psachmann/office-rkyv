using System.Text;

namespace OfficeRkyv.Api.Shared.Blobs;

/// <summary>
/// The text read from a file's content, kept beside the content it was read from:
/// <code>
/// &lt;directory&gt;/ab/abcdef....txt    UTF-8, one page after the other, separated by a form feed
/// </code>
/// A page that was not read is an empty page, so the n-th page of the text is the n-th page of the file. The
/// text is written into <c>tmp</c> first and renamed, as a blob is, so a reader never sees half a text.
/// </summary>
public sealed class TextStore
{
    /// <summary>Between two pages: U+000C, the convention of <c>pdftotext</c>.</summary>
    public const char PageSeparator = '\f';

    private const string Extension = ".txt";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string directory;
    private readonly string temporaryDirectory;

    public TextStore(string directory)
    {
        this.directory = directory;
        temporaryDirectory = Path.Combine(directory, FileSystemBlobStore.TemporaryFolder);
        Directory.CreateDirectory(temporaryDirectory);
    }

    /// <summary>
    /// Keeps the pages as the text of the content with this hash, replacing a text that is already kept: the
    /// extraction of a content that crashed after this write is done again, and writes the same pages.
    /// </summary>
    public async Task WriteAsync(string sha256, IEnumerable<string> pages, CancellationToken cancellation = default)
    {
        var target = PathOf(sha256) ?? throw new ArgumentException("Not a SHA-256 in lowercase hex.", nameof(sha256));
        var temporary = Path.Combine(temporaryDirectory, Guid.NewGuid().ToString("n"));

        try
        {
            await using (var writer = new StreamWriter(temporary, append: false, Utf8))
            {
                var first = true;

                foreach (var page in pages)
                {
                    if (!first)
                    {
                        await writer.WriteAsync(PageSeparator);
                    }

                    // A page cannot hold the separator, or the pages would not read back as they were written.
                    await writer.WriteAsync(page.Replace(PageSeparator, ' ').AsMemory(), cancellation);
                    first = false;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            // After a successful rename there is nothing here; after anything else this is the cleanup.
            File.Delete(temporary);
        }
    }

    /// <summary>Opens the text for reading, or returns null when no text is kept for this hash.</summary>
    public Stream? OpenRead(string sha256)
    {
        if (PathOf(sha256) is not { } path)
        {
            return null;
        }

        try
        {
            return new FileStream(
                path,
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

    /// <summary>The kept text as its pages in file order, or null when no text is kept for this hash.</summary>
    public async Task<IReadOnlyList<string>?> ReadPagesAsync(string sha256, CancellationToken cancellation = default)
    {
        await using var stream = OpenRead(sha256);

        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Utf8);
        var text = await reader.ReadToEndAsync(cancellation);

        return text.Split(PageSeparator);
    }

    private string? PathOf(string sha256) =>
        FileSystemBlobStore.IsSha256(sha256) ? Path.Combine(directory, sha256[..2], sha256 + Extension) : null;
}
