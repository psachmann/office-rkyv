using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UnitTests.Web;

public sealed class WorkspaceAddressesTests
{
    [Test]
    public async Task TheInbox_IsTheWorkspace() => await Assert.That(WorkspaceAddresses.Inbox).IsEqualTo("/workspace");

    [Test]
    public async Task AnArchive_IsUnderTheWorkspaceByItsId()
    {
        var id = Guid.NewGuid();

        await Assert.That(WorkspaceAddresses.Archive(id.ToString())).IsEqualTo($"/workspace/archives/{id}");
    }
}
