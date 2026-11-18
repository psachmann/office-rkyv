using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UnitTests.Web;

public sealed class AddressesTests
{
    [Test]
    public async Task TheInbox_IsItsOwnPage() => await Assert.That(Addresses.Inbox).IsEqualTo("/inbox");

    [Test]
    public async Task AnArchive_IsItsOwnPageByItsId()
    {
        var id = Guid.NewGuid();

        await Assert.That(Addresses.Archive(id.ToString())).IsEqualTo($"/archives/{id}");
    }
}
