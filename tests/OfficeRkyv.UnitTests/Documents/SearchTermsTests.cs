using OfficeRkyv.Contracts;

namespace OfficeRkyv.UnitTests.Documents;

public sealed class SearchTermsTests
{
    [Test]
    [Arguments("invoice april", new[] { "invoice", "april" })]
    [Arguments("  invoice   april  ", new[] { "invoice", "april" })]
    [Arguments("invoice\tapril\nmarch", new[] { "invoice", "april", "march" })]
    public async Task TheQuery_IsSplitOnAnyWhitespace(string query, string[] expected)
    {
        await Assert.That(SearchTerms.From(query)).IsEquivalentTo(expected);
    }

    [Test]
    public async Task AWordOfTwoCharacters_IsIgnored_AndOneOfThreeIsKept()
    {
        await Assert.That(SearchTerms.From("an inv")).IsEquivalentTo(["inv"]);
        await Assert.That(SearchTerms.From("an")).IsEmpty();
    }

    [Test]
    public async Task Digits_AreCountedAsCharacters()
    {
        await Assert.That(SearchTerms.From("47 471")).IsEquivalentTo(["471"]);
    }

    [Test]
    [Arguments("--- ...")]
    [Arguments("\"*\" !!!")]
    public async Task AWordWithNoLetterOrDigit_IsIgnored(string query)
    {
        await Assert.That(SearchTerms.From(query)).IsEmpty();
    }

    [Test]
    public async Task AWordWithPunctuationAndALetter_IsKept()
    {
        await Assert.That(SearchTerms.From("a-b")).IsEquivalentTo(["a-b"]);
    }

    [Test]
    public async Task AnUmlaut_IsOneCharacter()
    {
        await Assert.That(SearchTerms.From("äb")).IsEmpty();
        await Assert.That(SearchTerms.From("äbc")).IsEquivalentTo(["äbc"]);
    }

    [Test]
    public async Task ACharacterOutsideTheBasicPlane_IsOneCharacter()
    {
        // U+1D49C is two UTF-16 units; counted by units, "𝒜b" would pass as three characters.
        await Assert.That(SearchTerms.From("\U0001D49Cb")).IsEmpty();
        await Assert.That(SearchTerms.From("\U0001D49Cbc")).IsEquivalentTo(["\U0001D49Cbc"]);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("\t\n")]
    public async Task AnEmptyOrBlankQuery_HasNoWords(string query)
    {
        await Assert.That(SearchTerms.From(query)).IsEmpty();
    }
}
