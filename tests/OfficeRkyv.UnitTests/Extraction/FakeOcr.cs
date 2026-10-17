using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// Stands in for both OCR programs, so that the pipeline tests need none installed: it answers with prepared text
/// per page, can be told to fail on a page or to be unavailable as if the programs were not installed, and counts
/// its calls. The page images it "renders" are empty files, which the extractor deletes as it does real ones.
/// </summary>
public sealed class FakeOcr : IPageRenderer, IImageReader
{
    private readonly object gate = new();
    private readonly List<(string Sha256, int Page)> reads = [];
    private int renderCalls;

    /// <summary>As if the programs were not installed: every page is unread.</summary>
    public bool Unavailable { get; set; }

    /// <summary>The pages (from 1) on which OCR fails, rendering or reading.</summary>
    public HashSet<int> FailOnPages { get; } = [];

    /// <summary>What a page reads as; a page not listed reads as <c>ocr text of page N</c>.</summary>
    public Dictionary<int, string> TextOfPage { get; } = [];

    /// <summary>When set, a read waits for it, or for the host to stop: an extraction that is still running.</summary>
    public TaskCompletionSource? HoldReads { get; set; }

    /// <summary>Completed by the first read that starts, so a test knows the extraction is under way.</summary>
    public TaskCompletionSource FirstReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int RenderCalls => Volatile.Read(ref renderCalls);

    /// <summary>The reads so far, the content and the page of each, in order.</summary>
    public IReadOnlyList<(string Sha256, int Page)> Reads
    {
        get
        {
            lock (gate)
            {
                return [.. reads];
            }
        }
    }

    public int ReadCalls => Reads.Count;

    public Task<string?> RenderAsync(
        string sha256,
        string pdfPath,
        int pageNumber,
        string imagePathWithoutExtension,
        CancellationToken cancellation = default
    )
    {
        Interlocked.Increment(ref renderCalls);

        if (Unavailable || FailOnPages.Contains(pageNumber))
        {
            return Task.FromResult<string?>(null);
        }

        var image = imagePathWithoutExtension + ".png";
        File.WriteAllBytes(image, []);

        return Task.FromResult<string?>(image);
    }

    public async Task<string?> ReadAsync(
        string sha256,
        int pageNumber,
        string imagePath,
        CancellationToken cancellation = default
    )
    {
        lock (gate)
        {
            reads.Add((sha256, pageNumber));
        }

        FirstReadStarted.TrySetResult();

        if (HoldReads is { } hold)
        {
            // Ends when the host stops, as a real program does when the runner kills it.
            await hold.Task.WaitAsync(cancellation);
        }

        return Unavailable || FailOnPages.Contains(pageNumber)
            ? null
            : TextOfPage.GetValueOrDefault(pageNumber, $"ocr text of page {pageNumber}");
    }
}
