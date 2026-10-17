using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.UnitTests.Extraction;

public sealed class TextLayerRuleTests
{
    [Test]
    public async Task ATextLayerOfTwentyLetters_IsUsed()
    {
        await Assert.That(TextLayerRule.IsUsable(new string('a', 20))).IsTrue();
    }

    [Test]
    public async Task ATextLayerOfNineteenLetters_IsNot()
    {
        await Assert.That(TextLayerRule.IsUsable(new string('a', 19))).IsFalse();
    }

    [Test]
    public async Task NoTextLayer_IsNotUsed()
    {
        await Assert.That(TextLayerRule.IsUsable("")).IsFalse();
        await Assert.That(TextLayerRule.IsUsable(null)).IsFalse();
    }

    [Test]
    public async Task Digits_AreCounted()
    {
        await Assert.That(TextLayerRule.IsUsable("Rechnung 4711 vom 12032026")).IsTrue();
        await Assert.That(TextLayerRule.IsUsable(new string('7', 20))).IsTrue();
        await Assert.That(TextLayerRule.IsUsable(new string('7', 19))).IsFalse();
    }

    [Test]
    public async Task LettersWithAccents_AreCounted()
    {
        await Assert.That(TextLayerRule.IsUsable(new string('ä', 20))).IsTrue();
    }

    [Test]
    public async Task WhitespaceAndPunctuation_AreNotCounted()
    {
        // 19 letters, plus a lot that does not count.
        var text = string.Concat(Enumerable.Repeat("a. ", 19)) + "\n\t -- ,;:!? ()[]{}";

        await Assert.That(TextLayerRule.IsUsable(text)).IsFalse();
        await Assert.That(TextLayerRule.IsUsable(text + "b")).IsTrue();
    }

    [Test]
    public async Task APageNumberOrAStamp_IsNotEnough()
    {
        await Assert.That(TextLayerRule.IsUsable("Page 1")).IsFalse();
        await Assert.That(TextLayerRule.IsUsable("Scanned by HP-3140 - 1/2")).IsFalse();
    }
}
