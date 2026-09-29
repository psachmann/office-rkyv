using FluentValidation;
using JasperFx.Events;
using Microsoft.Extensions.DependencyInjection;
using OfficeRkyv.Api.Features.Archives;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Api.Features.Documents.Handlers;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.Api.Shared.Exceptions;
using OfficeRkyv.Contracts;
using Wolverine;

namespace OfficeRkyv.UnitTests.Documents;

public sealed class DocumentPipelineTests : IAsyncDisposable
{
    private PipelineHost pipeline = null!;

    private IMessageBus Bus => pipeline.Bus;

    [Before(Test)]
    public async Task SetUp() => pipeline = await PipelineHost.StartAsync();

    private Task<User> CreateUser(string email = "alice@example.com") =>
        Bus.InvokeAsync<User>(new CreateUserCommand(email, email));

    private Task<Archive> CreateArchive(User owner, string name = "Taxes") =>
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

    private async Task<int> EventCount(Guid streamId)
    {
        await using var session = pipeline.Store.QuerySession();

        return (await session.Events.FetchStreamAsync(streamId)).Count;
    }

    private async Task<IReadOnlyList<Document>> Inbox(User owner) =>
        await Bus.InvokeAsync<IReadOnlyList<Document>>(new FindInboxQuery(owner.Id));

    private async Task<IReadOnlyList<Document>> Contents(Archive archive) =>
        await Bus.InvokeAsync<IReadOnlyList<Document>>(new FindArchiveDocumentsQuery(archive.Id));

    private async Task File(Document document, Archive archive) =>
        await Bus.InvokeAsync<Document>(new FileDocumentCommand(document.Id, document.Version, archive.Id));

    // Puts the archive in the deleted state at event level, so these tests do not depend on the delete handler.
    private async Task MarkDeleted(Archive archive)
    {
        await using var session = pipeline.Store.LightweightSession();
        session.Events.Append(archive.Id, new ArchiveDeleted());
        await session.SaveChangesAsync();
    }

    private static async Task<ValidationException> ExpectValidationFailure(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (ValidationException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected the command to be rejected as invalid.");
    }

    private static IEnumerable<(string Property, string Code)> Violations(ValidationException failure) =>
        failure.Errors.Select(error => (error.PropertyName, error.ErrorCode));

    // ---- create ----

    [Test]
    public async Task Create_PutsTheDocumentInTheOwnersInbox()
    {
        var alice = await CreateUser();

        var created = await CreateDocument(alice, "Invoice March");

        await Assert.That(created.Title).IsEqualTo("Invoice March");
        await Assert.That(created.OwnerId).IsEqualTo(alice.Id);
        await Assert.That(created.ArchiveId).IsNull();
        await Assert.That(created.Version).IsEqualTo(1);
        await Assert.That((await Inbox(alice)).Select(document => document.Id)).IsEquivalentTo([created.Id]);
    }

    [Test]
    public async Task Create_StoresTheTitleTrimmed()
    {
        var alice = await CreateUser();

        var created = await CreateDocument(alice, "  Lease contract  ");

        await Assert.That(created.Title).IsEqualTo("Lease contract");
    }

    [Test]
    public async Task Create_ForAnUnknownOwner_IsNotFound()
    {
        await Assert
            .That(async () => await Bus.InvokeAsync<Document>(new CreateDocumentCommand(Guid.NewGuid(), "Invoice")))
            .Throws<NotFoundException>();
    }

    [Test]
    public async Task Create_ForADeletedOwner_ViolatesTheUserDeletedInvariant_AndCreatesNothing()
    {
        var alice = await CreateUser();
        await Bus.InvokeAsync(new DeleteUserCommand(alice.Id, alice.Version));

        var failure = await Assert.That(async () => await CreateDocument(alice)).Throws<InvariantViolationException>();

        await Assert.That(failure!.Code).IsEqualTo(InvariantCodes.UserDeleted);
        await Assert.That(await Inbox(alice)).IsEmpty();
    }

    [Test]
    public async Task Create_WithABlankTitle_IsRequired_AndCreatesNothing()
    {
        var alice = await CreateUser();

        var failure = await ExpectValidationFailure(() => CreateDocument(alice, "   "));

        await Assert
            .That(Violations(failure))
            .IsEquivalentTo([(nameof(CreateDocumentCommand.Title), ViolationCodes.Required)]);
        await Assert.That(await Inbox(alice)).IsEmpty();
    }

    [Test]
    public async Task Create_WithAnOverLongTitle_IsTooLong_ButTheLimitItselfIsAccepted()
    {
        var alice = await CreateUser();

        var failure = await ExpectValidationFailure(() =>
            CreateDocument(alice, new string('t', CreateDocumentValidator.MaxTitleLength + 1))
        );
        var atLimit = await CreateDocument(alice, new string('t', CreateDocumentValidator.MaxTitleLength));

        await Assert
            .That(Violations(failure))
            .IsEquivalentTo([(nameof(CreateDocumentCommand.Title), ViolationCodes.TooLong)]);
        await Assert.That(atLimit.Title.Length).IsEqualTo(CreateDocumentValidator.MaxTitleLength);
    }

    // ---- rename and delete ----

    [Test]
    public async Task Rename_WithTheCurrentVersion_ChangesTheTitle_AndRaisesTheVersion()
    {
        var alice = await CreateUser();
        var created = await CreateDocument(alice, "Invoice March");

        await Bus.InvokeAsync(new RenameDocumentCommand(created.Id, created.Version, "Invoice April"));

        var renamed = await Reload(created);
        await Assert.That(renamed.Title).IsEqualTo("Invoice April");
        await Assert.That(renamed.Version).IsGreaterThan(created.Version);
    }

    [Test]
    public async Task Rename_WithAStaleVersion_IsRejected_AndKeepsTheTitle()
    {
        var alice = await CreateUser();
        var created = await CreateDocument(alice, "Invoice March");
        await Bus.InvokeAsync(new RenameDocumentCommand(created.Id, created.Version, "Invoice April"));

        await Assert
            .That(async () => await Bus.InvokeAsync(new RenameDocumentCommand(created.Id, created.Version, "Late")))
            .Throws<EventStreamUnexpectedMaxEventIdException>();

        await Assert.That((await Reload(created)).Title).IsEqualTo("Invoice April");
    }

    [Test]
    public async Task Rename_ToAnOverLongTitle_IsTooLong_AndKeepsTheTitle()
    {
        var alice = await CreateUser();
        var created = await CreateDocument(alice, "Invoice");

        var failure = await ExpectValidationFailure(async () =>
            await Bus.InvokeAsync(
                new RenameDocumentCommand(
                    created.Id,
                    created.Version,
                    new string('t', CreateDocumentValidator.MaxTitleLength + 1)
                )
            )
        );

        await Assert
            .That(Violations(failure))
            .IsEquivalentTo([(nameof(RenameDocumentCommand.Title), ViolationCodes.TooLong)]);
        await Assert.That((await Reload(created)).Title).IsEqualTo("Invoice");
    }

    [Test]
    public async Task Rename_OfADeletedDocument_ViolatesTheDocumentDeletedInvariant()
    {
        var alice = await CreateUser();
        var created = await CreateDocument(alice);
        await Bus.InvokeAsync(new DeleteDocumentCommand(created.Id, created.Version));
        var deleted = await Reload(created);

        var failure = await Assert
            .That(async () => await Bus.InvokeAsync(new RenameDocumentCommand(created.Id, deleted.Version, "Back")))
            .Throws<InvariantViolationException>();

        await Assert.That(failure!.Code).IsEqualTo(InvariantCodes.DocumentDeleted);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task Rename_OfAnUnknownDocument_IsNotFound_WhateverVersionTheCallerSends(int version)
    {
        await Assert
            .That(async () => await Bus.InvokeAsync(new RenameDocumentCommand(Guid.NewGuid(), version, "Ghost")))
            .Throws<NotFoundException>();
    }

    [Test]
    public async Task Delete_RemovesTheDocumentFromTheInbox_AndDeletingAgainIsANoOp()
    {
        var alice = await CreateUser();
        var created = await CreateDocument(alice);

        await Bus.InvokeAsync(new DeleteDocumentCommand(created.Id, created.Version));
        var deleted = await Reload(created);
        var events = await EventCount(created.Id);

        await Bus.InvokeAsync(new DeleteDocumentCommand(created.Id, deleted.Version));

        await Assert.That(deleted.Status).IsEqualTo(EntityStatus.Deleted);
        await Assert.That(await Inbox(alice)).IsEmpty();
        await Assert.That(await EventCount(created.Id)).IsEqualTo(events);
    }

    [Test]
    public async Task Delete_WithAStaleVersion_IsRejected_AndTheDocumentStays()
    {
        var alice = await CreateUser();
        var created = await CreateDocument(alice);
        await Bus.InvokeAsync(new RenameDocumentCommand(created.Id, created.Version, "Renamed"));

        await Assert
            .That(async () => await Bus.InvokeAsync(new DeleteDocumentCommand(created.Id, created.Version)))
            .Throws<EventStreamUnexpectedMaxEventIdException>();

        await Assert.That(await Inbox(alice)).Count().IsEqualTo(1);
    }

    // ---- lists ----

    [Test]
    public async Task Inbox_HoldsOnlyTheOwnersUnfiledNonDeletedDocuments()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        var archive = await CreateArchive(alice);
        var unfiled = await CreateDocument(alice, "Unfiled");
        await File(await CreateDocument(alice, "Filed"), archive);
        var deleted = await CreateDocument(alice, "Deleted");
        await Bus.InvokeAsync(new DeleteDocumentCommand(deleted.Id, deleted.Version));
        await CreateDocument(bob, "Bobs");

        await Assert.That((await Inbox(alice)).Select(document => document.Id)).IsEquivalentTo([unfiled.Id]);
    }

    [Test]
    public async Task Inbox_ForAnUnknownOwner_IsEmpty()
    {
        await Assert.That(await Bus.InvokeAsync<IReadOnlyList<Document>>(new FindInboxQuery(Guid.NewGuid()))).IsEmpty();
    }

    [Test]
    public async Task ArchiveContents_AreOnlyItsNonDeletedMembers()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);
        var first = await CreateDocument(alice, "First");
        var second = await CreateDocument(alice, "Second");
        var gone = await CreateDocument(alice, "Gone");
        await CreateDocument(alice, "Outside");
        await File(first, archive);
        await File(second, archive);
        await File(gone, archive);
        await Bus.InvokeAsync(new DeleteDocumentCommand(gone.Id, (await Reload(gone)).Version));

        await Assert
            .That((await Contents(archive)).Select(document => document.Id))
            .IsEquivalentTo([first.Id, second.Id]);
    }

    [Test]
    public async Task ArchiveContents_OfAnEmptyOrUnknownArchive_AreEmpty()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);

        await Assert.That(await Contents(archive)).IsEmpty();
        await Assert
            .That(await Bus.InvokeAsync<IReadOnlyList<Document>>(new FindArchiveDocumentsQuery(Guid.NewGuid())))
            .IsEmpty();
    }

    // ---- filing ----

    [Test]
    public async Task Filing_FromTheInbox_MovesTheDocumentIntoTheArchive()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice, "Taxes 2026");
        var document = await CreateDocument(alice);

        var filed = await Bus.InvokeAsync<Document>(new FileDocumentCommand(document.Id, document.Version, archive.Id));

        await Assert.That(filed.ArchiveId).IsEqualTo(archive.Id);
        await Assert.That((await Contents(archive)).Select(d => d.Id)).IsEquivalentTo([document.Id]);
        await Assert.That(await Inbox(alice)).IsEmpty();
    }

    [Test]
    public async Task Filing_MovesTheArchivesVersion_SoARacingDeleteCannotMissTheDocument()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);
        var document = await CreateDocument(alice);

        await File(document, archive);

        await Assert.That((await Reload(archive)).Version).IsEqualTo(archive.Version + 1);
    }

    [Test]
    public async Task Filing_BetweenArchives_LeavesTheFirst()
    {
        var alice = await CreateUser();
        var household = await CreateArchive(alice, "Household");
        var taxes = await CreateArchive(alice, "Taxes 2026");
        var document = await CreateDocument(alice);
        await File(document, household);

        await File(await Reload(document), taxes);

        await Assert.That((await Contents(taxes)).Select(d => d.Id)).IsEquivalentTo([document.Id]);
        await Assert.That(await Contents(household)).IsEmpty();
        await Assert.That(await Inbox(alice)).IsEmpty();
    }

    [Test]
    public async Task Filing_IntoTheCurrentArchive_AppendsNothing()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);
        var document = await CreateDocument(alice);
        await File(document, archive);
        var filed = await Reload(document);
        var archiveAfterFiling = await Reload(archive);

        var again = await Bus.InvokeAsync<Document>(new FileDocumentCommand(filed.Id, filed.Version, archive.Id));

        await Assert.That(again.Version).IsEqualTo(filed.Version);
        await Assert.That(await EventCount(document.Id)).IsEqualTo(filed.Version);
        await Assert.That((await Reload(archive)).Version).IsEqualTo(archiveAfterFiling.Version);
    }

    [Test]
    public async Task Filing_IntoAnUnknownArchive_IsNotFound_AndTheDocumentStays()
    {
        var alice = await CreateUser();
        var document = await CreateDocument(alice);

        await Assert
            .That(async () =>
                await Bus.InvokeAsync<Document>(new FileDocumentCommand(document.Id, document.Version, Guid.NewGuid()))
            )
            .Throws<NotFoundException>();

        await Assert.That(await Inbox(alice)).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Filing_IntoADeletedArchive_ViolatesTheArchiveDeletedInvariant()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);
        var document = await CreateDocument(alice);
        await MarkDeleted(archive);

        var failure = await Assert
            .That(async () =>
                await Bus.InvokeAsync<Document>(new FileDocumentCommand(document.Id, document.Version, archive.Id))
            )
            .Throws<InvariantViolationException>();

        await Assert.That(failure!.Code).IsEqualTo(InvariantCodes.ArchiveDeleted);
        await Assert.That((await Reload(document)).ArchiveId).IsNull();
    }

    [Test]
    public async Task Filing_IntoAnotherUsersArchive_ViolatesTheOwnerMismatchInvariant()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        var bobsArchive = await CreateArchive(bob);
        var document = await CreateDocument(alice);

        var failure = await Assert
            .That(async () =>
                await Bus.InvokeAsync<Document>(new FileDocumentCommand(document.Id, document.Version, bobsArchive.Id))
            )
            .Throws<InvariantViolationException>();

        await Assert.That(failure!.Code).IsEqualTo(InvariantCodes.ArchiveOwnerMismatch);
        await Assert.That(await Inbox(alice)).Count().IsEqualTo(1);
        await Assert.That(await Contents(bobsArchive)).IsEmpty();
    }

    [Test]
    public async Task Filing_ADeletedDocument_ViolatesTheDocumentDeletedInvariant()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);
        var document = await CreateDocument(alice);
        await Bus.InvokeAsync(new DeleteDocumentCommand(document.Id, document.Version));
        var deleted = await Reload(document);

        var failure = await Assert
            .That(async () =>
                await Bus.InvokeAsync<Document>(new FileDocumentCommand(document.Id, deleted.Version, archive.Id))
            )
            .Throws<InvariantViolationException>();

        await Assert.That(failure!.Code).IsEqualTo(InvariantCodes.DocumentDeleted);
    }

    [Test]
    public async Task Filing_WithAStaleVersion_IsRejected_AndTheDocumentStays()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);
        var document = await CreateDocument(alice);
        await Bus.InvokeAsync(new RenameDocumentCommand(document.Id, document.Version, "Renamed"));

        await Assert
            .That(async () =>
                await Bus.InvokeAsync<Document>(new FileDocumentCommand(document.Id, document.Version, archive.Id))
            )
            .Throws<EventStreamUnexpectedMaxEventIdException>();

        await Assert.That(await Inbox(alice)).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Filing_AnUnknownDocument_IsNotFound()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);

        await Assert
            .That(async () => await Bus.InvokeAsync<Document>(new FileDocumentCommand(Guid.NewGuid(), 1, archive.Id)))
            .Throws<NotFoundException>();
    }

    // ---- unfiling ----

    [Test]
    public async Task Unfiling_ReturnsTheDocumentToTheInbox()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice, "Household");
        var document = await CreateDocument(alice);
        await File(document, archive);
        var filed = await Reload(document);

        await Bus.InvokeAsync(new UnfileDocumentCommand(filed.Id, filed.Version));

        await Assert.That((await Inbox(alice)).Select(d => d.Id)).IsEquivalentTo([document.Id]);
        await Assert.That(await Contents(archive)).IsEmpty();
    }

    [Test]
    public async Task Unfiling_AnInboxDocument_IsANoOp()
    {
        var alice = await CreateUser();
        var document = await CreateDocument(alice);

        await Bus.InvokeAsync(new UnfileDocumentCommand(document.Id, document.Version));

        await Assert.That(await EventCount(document.Id)).IsEqualTo(1);
        await Assert.That(await Inbox(alice)).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Unfiling_ADeletedDocument_ViolatesTheDocumentDeletedInvariant()
    {
        var alice = await CreateUser();
        var document = await CreateDocument(alice);
        await Bus.InvokeAsync(new DeleteDocumentCommand(document.Id, document.Version));
        var deleted = await Reload(document);

        var failure = await Assert
            .That(async () => await Bus.InvokeAsync(new UnfileDocumentCommand(document.Id, deleted.Version)))
            .Throws<InvariantViolationException>();

        await Assert.That(failure!.Code).IsEqualTo(InvariantCodes.DocumentDeleted);
    }

    // ---- returning to the inbox (the cascade's per-document step) ----

    [Test]
    public async Task ReturnToInbox_UnfilesADocumentStillInTheArchive()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice, "Household");
        var document = await CreateDocument(alice);
        await File(document, archive);

        await Bus.InvokeAsync(new ReturnDocumentToInbox(document.Id, archive.Id));

        await Assert.That((await Inbox(alice)).Select(d => d.Id)).IsEquivalentTo([document.Id]);
        await Assert.That(await Contents(archive)).IsEmpty();
    }

    [Test]
    public async Task ReturnToInbox_LeavesADocumentThatWasMovedElsewhere()
    {
        var alice = await CreateUser();
        var household = await CreateArchive(alice, "Household");
        var taxes = await CreateArchive(alice, "Taxes 2026");
        var document = await CreateDocument(alice);
        await File(document, household);
        await File(await Reload(document), taxes);
        var events = await EventCount(document.Id);

        await Bus.InvokeAsync(new ReturnDocumentToInbox(document.Id, household.Id));

        await Assert.That((await Contents(taxes)).Select(d => d.Id)).IsEquivalentTo([document.Id]);
        await Assert.That(await EventCount(document.Id)).IsEqualTo(events);
    }

    [Test]
    public async Task ReturnToInbox_LeavesADeletedDocumentDeleted()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);
        var document = await CreateDocument(alice);
        await File(document, archive);
        await Bus.InvokeAsync(new DeleteDocumentCommand(document.Id, (await Reload(document)).Version));
        var events = await EventCount(document.Id);

        await Bus.InvokeAsync(new ReturnDocumentToInbox(document.Id, archive.Id));

        await Assert.That(await Inbox(alice)).IsEmpty();
        await Assert.That(await EventCount(document.Id)).IsEqualTo(events);
    }

    [Test]
    public async Task ReturnToInbox_IsIdempotent()
    {
        var alice = await CreateUser();
        var archive = await CreateArchive(alice);
        var document = await CreateDocument(alice);
        await File(document, archive);

        await Bus.InvokeAsync(new ReturnDocumentToInbox(document.Id, archive.Id));
        var events = await EventCount(document.Id);
        await Bus.InvokeAsync(new ReturnDocumentToInbox(document.Id, archive.Id));

        await Assert.That(await EventCount(document.Id)).IsEqualTo(events);
    }

    public async ValueTask DisposeAsync() => await pipeline.DisposeAsync();
}
