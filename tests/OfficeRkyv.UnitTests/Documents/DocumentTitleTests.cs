using OfficeRkyv.Api.Features.Documents;

namespace OfficeRkyv.UnitTests.Documents;

public sealed class DocumentTitleTests
{
    [Test]
    [Arguments("Invoice March.pdf", "Invoice March")]
    [Arguments("  Invoice March.pdf  ", "Invoice March")]
    [Arguments("backup.2026.tar.gz", "backup.2026.tar")]
    [Arguments("README", "README")]
    [Arguments("notes.", "notes")]
    public async Task TheTitle_IsTheFileNameWithoutItsLastExtension(string fileName, string expected)
    {
        await Assert.That(DocumentTitle.FromFileName(fileName)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(".pdf")]
    [Arguments(".gitignore")]
    public async Task ANameThatIsOnlyAnExtension_IsUsedWhole(string fileName)
    {
        await Assert.That(DocumentTitle.FromFileName(fileName)).IsEqualTo(fileName);
    }

    [Test]
    public async Task ALongName_IsCutToTheTitleLimit()
    {
        var title = DocumentTitle.FromFileName(new string('t', 230) + ".pdf");

        await Assert.That(title).IsEqualTo(new string('t', 200));
    }

    [Test]
    public async Task TheCut_NeverSplitsASurrogatePair()
    {
        // 199 letters, then one character that is two UTF-16 units: cutting at 200 would keep half of it.
        var title = DocumentTitle.FromFileName(new string('t', 199) + "😀" + "more.pdf");

        await Assert.That(title).IsEqualTo(new string('t', 199));
        await Assert.That(char.IsSurrogate(title[^1])).IsFalse();
    }
}
