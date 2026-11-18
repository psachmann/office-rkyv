using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UnitTests.Web;

public sealed class ArchiveResolutionTests
{
    private static ArchiveDto Archive(Guid id, string name = "Taxes") =>
        new()
        {
            Id = id.ToString(),
            Name = name,
            OwnerId = Guid.NewGuid().ToString(),
        };

    [Test]
    public async Task AnArchiveInTheList_IsFound()
    {
        var id = Guid.NewGuid();

        await Assert
            .That(ArchiveResolution.Resolve([Archive(id)], id, seenBefore: false))
            .IsEqualTo(ArchiveLookup.Found);
    }

    [Test]
    public async Task AnIdWrittenInUpperCase_IsFound()
    {
        var id = Guid.NewGuid();
        var upper = new ArchiveDto
        {
            Id = id.ToString().ToUpperInvariant(),
            Name = "Taxes",
            OwnerId = Guid.NewGuid().ToString(),
        };

        await Assert.That(ArchiveResolution.Resolve([upper], id, seenBefore: false)).IsEqualTo(ArchiveLookup.Found);
    }

    [Test]
    public async Task AnIdNotInTheList_NeverShown_IsNotFound()
    {
        await Assert
            .That(ArchiveResolution.Resolve([Archive(Guid.NewGuid())], Guid.NewGuid(), seenBefore: false))
            .IsEqualTo(ArchiveLookup.NotFound);
    }

    [Test]
    public async Task AnIdNotInTheList_ShownBefore_HasVanished()
    {
        await Assert
            .That(ArchiveResolution.Resolve([], Guid.NewGuid(), seenBefore: true))
            .IsEqualTo(ArchiveLookup.Vanished);
    }

    [Test]
    public async Task AnEmptyListWithANeverSeenId_IsNotFound()
    {
        await Assert
            .That(ArchiveResolution.Resolve([], Guid.NewGuid(), seenBefore: false))
            .IsEqualTo(ArchiveLookup.NotFound);
    }
}
