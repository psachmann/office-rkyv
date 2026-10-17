using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using OfficeRkyv.Api.Shared.Blobs;

namespace OfficeRkyv.UnitTests.Blobs;

public sealed class FileSystemBlobStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.blobs");
    private readonly FileSystemBlobStore store;

    public FileSystemBlobStoreTests() => store = new FileSystemBlobStore(directory);

    private string TemporaryDirectory => Path.Combine(directory, "tmp");

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks(byte[] content, int chunkSize)
    {
        for (var offset = 0; offset < content.Length; offset += chunkSize)
        {
            await Task.Yield();

            yield return content.AsMemory(offset, Math.Min(chunkSize, content.Length - offset));
        }
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> FailingAfter(byte[] content)
    {
        yield return content;
        await Task.Yield();

        throw new IOException("The connection broke.");
    }

    private static string Sha256Of(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    private IEnumerable<string> StoredFiles() =>
        Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(TemporaryDirectory));

    [Test]
    public async Task Content_IsStoredByItsHash_AndReadBackUnchanged()
    {
        var content = TestDocuments.Bytes(200_000);

        var result = await store.WriteAsync(Chunks(content, 64 * 1024), maxBytes: 1_000_000);

        await Assert.That(result.Status).IsEqualTo(BlobWriteStatus.Stored);
        await Assert.That(result.Sha256).IsEqualTo(Sha256Of(content));
        await Assert.That(result.Size).IsEqualTo(content.Length);

        await using var stream = store.OpenRead(result.Sha256!);
        using var read = new MemoryStream();
        await stream!.CopyToAsync(read);
        await Assert.That(read.ToArray()).IsEquivalentTo(content);
    }

    [Test]
    public async Task TheSameContentTwice_IsStoredOnce_UnderTheSameHash()
    {
        var content = TestDocuments.Bytes(1_000);

        var first = await store.WriteAsync(Chunks(content, 300), maxBytes: 10_000);
        var second = await store.WriteAsync(Chunks(content, 700), maxBytes: 10_000);

        await Assert.That(second.Sha256).IsEqualTo(first.Sha256);
        await Assert.That(StoredFiles().Count()).IsEqualTo(1);
        await Assert.That(Directory.EnumerateFiles(TemporaryDirectory)).IsEmpty();
    }

    [Test]
    public async Task EmptyContent_IsReportedAsEmpty_AndLeavesNothing()
    {
        var result = await store.WriteAsync(Chunks([], 10), maxBytes: 10_000);

        await Assert.That(result.Status).IsEqualTo(BlobWriteStatus.Empty);
        await Assert.That(StoredFiles()).IsEmpty();
        await Assert.That(Directory.EnumerateFiles(TemporaryDirectory)).IsEmpty();
    }

    [Test]
    public async Task ContentAtTheLimit_IsStored_AndOneByteOverIsTooLarge_AndLeavesNothing()
    {
        var atLimit = await store.WriteAsync(Chunks(TestDocuments.Bytes(1_000), 400), maxBytes: 1_000);
        var overLimit = await store.WriteAsync(Chunks(TestDocuments.Bytes(1_001), 400), maxBytes: 1_000);

        await Assert.That(atLimit.Status).IsEqualTo(BlobWriteStatus.Stored);
        await Assert.That(overLimit.Status).IsEqualTo(BlobWriteStatus.TooLarge);
        await Assert.That(StoredFiles().Count()).IsEqualTo(1);
        await Assert.That(Directory.EnumerateFiles(TemporaryDirectory)).IsEmpty();
    }

    [Test]
    public async Task ContentThatFailsHalfWay_LeavesNothingBehind()
    {
        await Assert
            .That(async () => await store.WriteAsync(FailingAfter(TestDocuments.Bytes(500)), maxBytes: 10_000))
            .Throws<IOException>();

        await Assert.That(StoredFiles()).IsEmpty();
        await Assert.That(Directory.EnumerateFiles(TemporaryDirectory)).IsEmpty();
    }

    [Test]
    public async Task AnUnknownHash_OpensNothing_AndSoDoesAnythingThatIsNotAHash()
    {
        await Assert.That(store.OpenRead(new string('a', 64))).IsNull();
        await Assert.That(store.OpenRead("../../etc/passwd")).IsNull();
        await Assert.That(store.OpenRead("")).IsNull();
    }

    [Test]
    public async Task AStoredBlob_HasAPath_WhoseFileHoldsTheContent()
    {
        var content = TestDocuments.Bytes(500);
        var result = await store.WriteAsync(Chunks(content, 200), maxBytes: 10_000);

        var path = store.PathOfStored(result.Sha256!);

        await Assert.That(path).IsEqualTo(Path.Combine(directory, result.Sha256![..2], result.Sha256));
        await Assert.That(File.ReadAllBytes(path!)).IsEquivalentTo(content);
    }

    [Test]
    public async Task AnUnknownHash_HasNoPath_AndSoDoesAnythingThatIsNotAHash()
    {
        await Assert.That(store.PathOfStored(new string('a', 64))).IsNull();
        await Assert.That(store.PathOfStored("../../etc/passwd")).IsNull();
        await Assert.That(store.PathOfStored("")).IsNull();
    }

    [Test]
    public async Task ATemporaryPath_IsInTmp_AndIsNewEachTime()
    {
        var first = store.NewTemporaryPath();
        var second = store.NewTemporaryPath();

        await Assert.That(Path.GetDirectoryName(first)).IsEqualTo(TemporaryDirectory);
        await Assert.That(second).IsNotEqualTo(first);
    }

    [Test]
    public async Task LeftoversOfACrash_AreRemovedWhenTheHostStarts_AndOneWarningSaysHowMany()
    {
        File.WriteAllText(Path.Combine(TemporaryDirectory, "half-written-1"), "x");
        File.WriteAllText(Path.Combine(TemporaryDirectory, "half-written-2"), "x");
        var logger = new CapturingLogger<BlobStoreCleanupService>();

        await new BlobStoreCleanupService(store, logger).StartAsync(CancellationToken.None);

        await Assert.That(Directory.EnumerateFiles(TemporaryDirectory)).IsEmpty();
        var entry = logger.Entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).Contains("2");
    }

    [Test]
    public async Task StartingWithNothingLeftOver_LogsNothing()
    {
        var logger = new CapturingLogger<BlobStoreCleanupService>();

        await new BlobStoreCleanupService(store, logger).StartAsync(CancellationToken.None);

        await Assert.That(logger.Entries).IsEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
