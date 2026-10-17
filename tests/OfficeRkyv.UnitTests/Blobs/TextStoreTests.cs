using OfficeRkyv.Api.Shared.Blobs;

namespace OfficeRkyv.UnitTests.Blobs;

public sealed class TextStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.blobs");
    private readonly TextStore store;

    private static readonly string Hash = new('a', 64);

    public TextStoreTests() => store = new TextStore(directory);

    private string TemporaryDirectory => Path.Combine(directory, "tmp");

    private static IEnumerable<string> FailingAfter(string firstPage)
    {
        yield return firstPage;

        throw new IOException("The disk broke.");
    }

    [Test]
    public async Task Pages_AreReadBackAsTheyWereWritten()
    {
        await store.WriteAsync(Hash, ["first page\nwith two lines", "Größe: 5 €", "third"]);

        var pages = await store.ReadPagesAsync(Hash);

        await Assert.That(pages).IsEquivalentTo(["first page\nwith two lines", "Größe: 5 €", "third"]);
    }

    [Test]
    public async Task AnEmptyPage_KeepsItsPosition()
    {
        await store.WriteAsync(Hash, ["first", "", "third"]);

        var pages = await store.ReadPagesAsync(Hash);

        await Assert.That(pages!.Count).IsEqualTo(3);
        await Assert.That(pages[0]).IsEqualTo("first");
        await Assert.That(pages[1]).IsEmpty();
        await Assert.That(pages[2]).IsEqualTo("third");
    }

    [Test]
    public async Task AnEmptyLastPage_IsKeptToo()
    {
        await store.WriteAsync(Hash, ["first", ""]);

        await Assert.That((await store.ReadPagesAsync(Hash))!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task TheText_IsStoredBesideItsBlobsPlace_AsUtf8WithFormFeedsBetweenPages()
    {
        await store.WriteAsync(Hash, ["one", "zwei"]);

        var file = Path.Combine(directory, "aa", Hash + ".txt");
        await Assert.That(File.ReadAllBytes(file)).IsEquivalentTo("one\fzwei"u8.ToArray());
    }

    [Test]
    public async Task AFormFeedInsideAPage_DoesNotSplitIt()
    {
        await store.WriteAsync(Hash, ["one\ftwo", "three"]);

        var pages = await store.ReadPagesAsync(Hash);

        await Assert.That(pages!.Count).IsEqualTo(2);
        await Assert.That(pages[1]).IsEqualTo("three");
    }

    [Test]
    public async Task WritingAgain_ReplacesTheText()
    {
        await store.WriteAsync(Hash, ["old"]);
        await store.WriteAsync(Hash, ["new", "newer"]);

        await Assert.That(await store.ReadPagesAsync(Hash)).IsEquivalentTo(["new", "newer"]);
        await Assert.That(Directory.EnumerateFiles(TemporaryDirectory)).IsEmpty();
    }

    [Test]
    public async Task AWriteThatFailsHalfWay_LeavesNothingBehind_AndKeepsTheEarlierText()
    {
        await store.WriteAsync(Hash, ["earlier"]);

        await Assert.That(async () => await store.WriteAsync(Hash, FailingAfter("half"))).Throws<IOException>();

        await Assert.That(Directory.EnumerateFiles(TemporaryDirectory)).IsEmpty();
        await Assert.That(await store.ReadPagesAsync(Hash)).IsEquivalentTo(["earlier"]);
    }

    [Test]
    public async Task AFailedFirstWrite_KeepsNoText()
    {
        await Assert.That(async () => await store.WriteAsync(Hash, FailingAfter("half"))).Throws<IOException>();

        await Assert.That(Directory.EnumerateFiles(TemporaryDirectory)).IsEmpty();
        await Assert.That(await store.ReadPagesAsync(Hash)).IsNull();
        await Assert.That(store.OpenRead(Hash)).IsNull();
    }

    [Test]
    public async Task AnUnknownHash_ReadsNothing_AndSoDoesAnythingThatIsNotAHash()
    {
        await Assert.That(await store.ReadPagesAsync(new string('b', 64))).IsNull();
        await Assert.That(store.OpenRead(new string('b', 64))).IsNull();
        await Assert.That(store.OpenRead("../../etc/passwd")).IsNull();
        await Assert.That(await store.ReadPagesAsync("")).IsNull();
    }

    [Test]
    public async Task AWriteUnderANameThatIsNotAHash_IsRefused()
    {
        await Assert.That(async () => await store.WriteAsync("../../escape", ["x"])).Throws<ArgumentException>();
    }

    [Test]
    public async Task TheStreamOfAKeptText_ReadsTheSameBytes()
    {
        await store.WriteAsync(Hash, ["one", "two"]);

        await using var stream = store.OpenRead(Hash);
        using var read = new MemoryStream();
        await stream!.CopyToAsync(read);

        await Assert.That(read.ToArray()).IsEquivalentTo("one\ftwo"u8.ToArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
