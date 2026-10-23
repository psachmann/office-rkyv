using OfficeRkyv.Api.Features.TextExtraction;

namespace OfficeRkyv.UnitTests.Extraction;

public sealed class ContentTextTests
{
    private const string Hash = "ab";

    [Test]
    public async Task NoPages_GiveAnEmptyText_KeyedByTheHash()
    {
        var text = ContentText.From(Hash, []);

        await Assert.That(text.Id).IsEqualTo(Hash);
        await Assert.That(text.Text).IsEmpty();
    }

    [Test]
    public async Task OnePage_IsTheText()
    {
        await Assert.That(ContentText.From(Hash, ["Invoice 4711"]).Text).IsEqualTo("Invoice 4711");
    }

    [Test]
    public async Task Pages_AreJoinedInOrder_OnSeparateLines()
    {
        await Assert.That(ContentText.From(Hash, ["first", "second", "third"]).Text).IsEqualTo("first\nsecond\nthird");
    }

    [Test]
    public async Task APageThatWasNotRead_AddsNothing()
    {
        await Assert.That(ContentText.From(Hash, ["first", "", "third"]).Text).IsEqualTo("first\nthird");
    }

    [Test]
    public async Task APageOfOnlyWhitespace_AddsNothing()
    {
        await Assert.That(ContentText.From(Hash, ["first", " \n\t ", "third"]).Text).IsEqualTo("first\nthird");
    }
}
