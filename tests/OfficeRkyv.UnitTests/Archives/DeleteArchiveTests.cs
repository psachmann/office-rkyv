using JasperFx.Events;
using OfficeRkyv.Api.Features.Archives;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Shared;
using Wolverine;

namespace OfficeRkyv.UnitTests.Archives;

/// <summary>Deleting an archive and the cascade that returns its documents to the inbox.</summary>
public sealed class DeleteArchiveTests : IAsyncDisposable
{
    private PipelineHost pipeline = null!;

    private IMessageBus Bus => pipeline.Bus;

    [Before(Test)]
    public async Task SetUp() => pipeline = await PipelineHost.StartAsync();

    private Task<User> CreateUser(string email = "alice@example.com") =>
        Bus.InvokeAsync<User>(new CreateUserCommand(email, email));

    private Task<Archive> CreateArchive(User owner, string name = "Household") =>
        Bus.InvokeAsync<Archive>(new CreateArchiveCommand(owner.Id, name));

    private Task<Document> CreateDocument(User owner, string title = "Invoice") =>
        Bus.InvokeAsync<Document>(new CreateDocumentCommand(owner.Id, title));

    private async Task<Document> Reload(Document document)
    {
        await using var session = pipeline.Store.QuerySession();

        return (await session.LoadAsync<Document>(document.Id))!;
    }

    private async Task<Archive> Reload(Archive archive)
    {
        await using var session = pipeline.Store.QuerySession();

        return (await session.LoadAsync<Archive>(archive.Id))!;
    }

    private async Task File(Document document, Archive archive)
    {
        var current = await Reload(document);

        await Bus.InvokeAsync<Document>(new FileDocumentCommand(current.Id, current.Version, archive.Id));
    }

    private async Task<IReadOnlyList<Document>> Inbox(User owner) =>
        await Bus.InvokeAsync<IReadOnlyList<Document>>(new FindInboxQuery(owner.Id));

    private async Task<IReadOnlyList<Archive>> ArchivesOf(User owner) =>
        await Bus.InvokeAsync<IReadOnlyList<Archive>>(new FindArchivesByOwnerQuery(owner.Id));

    private async Task<int> EventCount(Guid streamId)
    {
        await using var session = pipeline.Store.QuerySession();

        return (await session.Events.FetchStreamAsync(streamId)).Count;
    }

    [Test]
    public async Task Deleting_AnEmptyArchive_RemovesItFromTheOwnersList()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);

        await pipeline.InvokeAndSettleAsync(new DeleteArchiveCommand(archive.Id, archive.Version));

        await Assert.That(await ArchivesOf(alice)).IsEmpty();
        await Assert.That((await Reload(archive)).Status).IsEqualTo(EntityStatus.Deleted);
    }

    [Test]
    public async Task Deleting_ANonEmptyArchive_ReturnsAllItsDocumentsToTheInbox_OnceSettled()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);
        var first = await CreateDocument(alice, "First");
        var second = await CreateDocument(alice, "Second");
        await File(first, archive);
        await File(second, archive);
        var current = await Reload(archive);

        await pipeline.InvokeAndSettleAsync(new DeleteArchiveCommand(archive.Id, current.Version));

        await Assert.That(await ArchivesOf(alice)).IsEmpty();
        await Assert.That((await Inbox(alice)).Select(d => d.Id)).IsEquivalentTo([first.Id, second.Id]);
        await Assert.That((await Reload(first)).ArchiveId).IsNull();
        await Assert.That((await Reload(second)).ArchiveId).IsNull();
    }

    [Test]
    public async Task Deleting_LeavesOtherArchivesDocumentsAndDeletedOnesAlone()
    {
        var alice = await CreateUser();
        var doomed = await CreateArchive(alice, "Doomed");
        var keeper = await CreateArchive(alice, "Keeper");
        var inDoomed = await CreateDocument(alice, "In doomed");
        var inKeeper = await CreateDocument(alice, "In keeper");
        var deleted = await CreateDocument(alice, "Deleted");
        await File(inDoomed, doomed);
        await File(inKeeper, keeper);
        await File(deleted, doomed);
        await Bus.InvokeAsync(new DeleteDocumentCommand(deleted.Id, (await Reload(deleted)).Version));
        var deletedEvents = await EventCount(deleted.Id);

        await pipeline.InvokeAndSettleAsync(new DeleteArchiveCommand(doomed.Id, (await Reload(doomed)).Version));

        await Assert.That((await Inbox(alice)).Select(d => d.Id)).IsEquivalentTo([inDoomed.Id]);
        await Assert.That((await Reload(inKeeper)).ArchiveId).IsEqualTo(keeper.Id);
        await Assert.That(await EventCount(deleted.Id)).IsEqualTo(deletedEvents);
    }

    [Test]
    public async Task Deleting_AnAlreadyDeletedArchive_IsANoOp()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);
        await pipeline.InvokeAndSettleAsync(new DeleteArchiveCommand(archive.Id, archive.Version));
        var deleted = await Reload(archive);
        var events = await EventCount(archive.Id);

        await pipeline.InvokeAndSettleAsync(new DeleteArchiveCommand(archive.Id, deleted.Version));

        await Assert.That(await EventCount(archive.Id)).IsEqualTo(events);
    }

    [Test]
    public async Task Deleting_WithAStaleVersion_IsRejected_AndTheArchiveAndItsDocumentsStay()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);
        var document = await CreateDocument(alice);
        await File(document, archive);
        // Filing moved the archive's version, so the version from creation is stale.

        await Assert
            .That(async () =>
                await pipeline.InvokeAndSettleAsync(new DeleteArchiveCommand(archive.Id, archive.Version))
            )
            .Throws<EventStreamUnexpectedMaxEventIdException>();

        await Assert.That(await ArchivesOf(alice)).Count().IsEqualTo(1);
        await Assert.That((await Reload(document)).ArchiveId).IsEqualTo(archive.Id);
    }

    [Test]
    public async Task Deleting_AnUnknownArchive_IsNotFound()
    {
        await Assert
            .That(async () => await Bus.InvokeAsync(new DeleteArchiveCommand(Guid.NewGuid(), 1)))
            .Throws<OfficeRkyv.Api.Shared.Exceptions.NotFoundException>();
    }

    public async ValueTask DisposeAsync() => await pipeline.DisposeAsync();
}
