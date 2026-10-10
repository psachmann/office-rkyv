using OfficeRkyv.Api.Shared.Blobs;

namespace OfficeRkyv.UnitTests.Blobs;

public sealed class BlobDirectoryTests
{
    [Test]
    public async Task WithoutAConfiguredDirectory_ItIsNamedAfterTheDatabaseFile_AndBesideIt()
    {
        var directory = BlobDirectory.For("Data Source=/tmp/0cfwqg5j.3ah.db", configured: null);

        await Assert.That(directory).IsEqualTo("/tmp/0cfwqg5j.3ah.db.blobs");
    }

    [Test]
    [Arguments("Data Source=/var/lib/office-rkyv/office-rkyv.db;Cache=Shared")]
    [Arguments("Filename=/var/lib/office-rkyv/office-rkyv.db")]
    public async Task OtherSpellingsOfTheConnectionString_GiveTheSameDirectory(string connectionString)
    {
        await Assert
            .That(BlobDirectory.For(connectionString, configured: ""))
            .IsEqualTo("/var/lib/office-rkyv/office-rkyv.db.blobs");
    }

    [Test]
    public async Task AConfiguredDirectory_WinsOverTheDatabase()
    {
        await Assert.That(BlobDirectory.For("Data Source=/tmp/a.db", configured: "/srv/files")).IsEqualTo("/srv/files");
    }

    [Test]
    public async Task ADatabaseThatIsNotAFile_NeedsAConfiguredDirectory()
    {
        await Assert
            .That(() => BlobDirectory.For("Data Source=:memory:", configured: null))
            .Throws<InvalidOperationException>();
    }
}
