using FluentValidation;
using JasperFx.Events;
using Microsoft.Extensions.DependencyInjection;
using OfficeRkyv.Api.Features.Archives;
using OfficeRkyv.Api.Features.Archives.Handlers;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.Api.Shared.Exceptions;
using OfficeRkyv.Contracts;
using Wolverine;

namespace OfficeRkyv.UnitTests.Archives;

public sealed class ArchivePipelineTests : IAsyncDisposable
{
    private PipelineHost pipeline = null!;

    private IMessageBus Bus => pipeline.Bus;

    [Before(Test)]
    public async Task SetUp() => pipeline = await PipelineHost.StartAsync();

    private Task<User> CreateUser(string email = "alice@example.com") =>
        Bus.InvokeAsync<User>(new CreateUserCommand(email, email));

    private Task<Archive> CreateArchive(User owner, string name) =>
        Bus.InvokeAsync<Archive>(new CreateArchiveCommand(owner.Id, name));

    private Task<IReadOnlyList<Archive>> ArchivesOf(User owner) =>
        Bus.InvokeAsync<IReadOnlyList<Archive>>(new FindArchivesByOwnerQuery(owner.Id));

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

    [Test]
    public async Task Create_ReturnsTheArchiveOwnedByTheUser_AndListsIt()
    {
        var alice = await CreateUser();

        var created = await CreateArchive(alice, "Taxes 2026");

        await Assert.That(created.Name).IsEqualTo("Taxes 2026");
        await Assert.That(created.OwnerId).IsEqualTo(alice.Id);
        await Assert.That(created.Version).IsEqualTo(1);
        await Assert.That((await ArchivesOf(alice)).Select(archive => archive.Id)).IsEquivalentTo([created.Id]);
    }

    [Test]
    public async Task Create_StoresTheNameTrimmed()
    {
        var alice = await CreateUser();

        var created = await CreateArchive(alice, "  Taxes  ");

        await Assert.That(created.Name).IsEqualTo("Taxes");
    }

    [Test]
    public async Task ANewUser_HasNoArchives()
    {
        var alice = await CreateUser();

        await Assert.That(await ArchivesOf(alice)).IsEmpty();
    }

    [Test]
    public async Task Create_WithABlankName_IsRequired_AndStoresNothing()
    {
        var alice = await CreateUser();

        var failure = await ExpectValidationFailure(() => CreateArchive(alice, "   "));

        await Assert
            .That(Violations(failure))
            .IsEquivalentTo([(nameof(CreateArchiveCommand.Name), ViolationCodes.Required)]);
        await Assert.That(await ArchivesOf(alice)).IsEmpty();
    }

    [Test]
    public async Task Create_WithAnOverLongName_IsTooLong_ButTheLimitItselfIsAccepted()
    {
        var alice = await CreateUser();

        var failure = await ExpectValidationFailure(() =>
            CreateArchive(alice, new string('n', CreateArchiveValidator.MaxNameLength + 1))
        );
        var atLimit = await CreateArchive(alice, new string('n', CreateArchiveValidator.MaxNameLength));

        await Assert
            .That(Violations(failure))
            .IsEquivalentTo([(nameof(CreateArchiveCommand.Name), ViolationCodes.TooLong)]);
        await Assert.That(atLimit.Name.Length).IsEqualTo(CreateArchiveValidator.MaxNameLength);
    }

    [Test]
    public async Task Create_ForAnUnknownOwner_IsNotFound()
    {
        await Assert
            .That(async () => await Bus.InvokeAsync<Archive>(new CreateArchiveCommand(Guid.NewGuid(), "Taxes")))
            .Throws<NotFoundException>();
    }

    [Test]
    public async Task Create_ForADeletedOwner_ViolatesTheUserDeletedInvariant()
    {
        var alice = await CreateUser();
        await Bus.InvokeAsync(new DeleteUserCommand(alice.Id, alice.Version));

        var failure = await Assert
            .That(async () => await Bus.InvokeAsync<Archive>(new CreateArchiveCommand(alice.Id, "Taxes")))
            .Throws<InvariantViolationException>();

        await Assert.That(failure!.Code).IsEqualTo(InvariantCodes.UserDeleted);
        await Assert.That(await ArchivesOf(alice)).IsEmpty();
    }

    [Test]
    public async Task DuplicateNames_AreAllowed()
    {
        var alice = await CreateUser();

        var first = await CreateArchive(alice, "Taxes");
        var second = await CreateArchive(alice, "Taxes");

        await Assert.That(second.Id).IsNotEqualTo(first.Id);
        await Assert.That((await ArchivesOf(alice)).Select(archive => archive.Name)).IsEquivalentTo(["Taxes", "Taxes"]);
    }

    [Test]
    public async Task Rename_WithTheCurrentVersion_ChangesTheName()
    {
        var alice = await CreateUser();
        var created = await CreateArchive(alice, "Taxes");

        await Bus.InvokeAsync(new RenameArchiveCommand(created.Id, created.Version, "Taxes 2026"));

        var renamed = (await ArchivesOf(alice)).Single();
        await Assert.That(renamed.Name).IsEqualTo("Taxes 2026");
        await Assert.That(renamed.Version).IsEqualTo(2);
    }

    [Test]
    public async Task Rename_WithAStaleVersion_IsRejected_AndKeepsTheName()
    {
        var alice = await CreateUser();
        var created = await CreateArchive(alice, "Taxes");
        await Bus.InvokeAsync(new RenameArchiveCommand(created.Id, created.Version, "Taxes 2026"));

        await Assert
            .That(async () => await Bus.InvokeAsync(new RenameArchiveCommand(created.Id, created.Version, "Late")))
            .Throws<EventStreamUnexpectedMaxEventIdException>();

        await Assert.That((await ArchivesOf(alice)).Single().Name).IsEqualTo("Taxes 2026");
    }

    [Test]
    public async Task Rename_ToABlankName_IsRequired()
    {
        var alice = await CreateUser();
        var created = await CreateArchive(alice, "Taxes");

        var failure = await ExpectValidationFailure(async () =>
            await Bus.InvokeAsync(new RenameArchiveCommand(created.Id, created.Version, " "))
        );

        await Assert
            .That(Violations(failure))
            .IsEquivalentTo([(nameof(RenameArchiveCommand.Name), ViolationCodes.Required)]);
    }

    [Test]
    public async Task Rename_OfADeletedArchive_ViolatesTheArchiveDeletedInvariant()
    {
        var alice = await CreateUser();
        var created = await CreateArchive(alice, "Taxes");
        await MarkDeleted(created);

        var failure = await Assert
            .That(async () => await Bus.InvokeAsync(new RenameArchiveCommand(created.Id, created.Version + 1, "Back")))
            .Throws<InvariantViolationException>();

        await Assert.That(failure!.Code).IsEqualTo(InvariantCodes.ArchiveDeleted);
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task Rename_OfAnUnknownArchive_IsNotFound_WhateverVersionTheCallerSends(int version)
    {
        await Assert
            .That(async () => await Bus.InvokeAsync(new RenameArchiveCommand(Guid.NewGuid(), version, "Ghost")))
            .Throws<NotFoundException>();
    }

    [Test]
    public async Task List_HoldsOnlyTheOwnersNonDeletedArchives()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        await CreateArchive(alice, "Taxes");
        await CreateArchive(alice, "Household");
        await CreateArchive(bob, "Work");
        await MarkDeleted(await CreateArchive(alice, "Gone"));

        await Assert
            .That((await ArchivesOf(alice)).Select(archive => archive.Name))
            .IsEquivalentTo(["Taxes", "Household"]);
        await Assert.That((await ArchivesOf(bob)).Select(archive => archive.Name)).IsEquivalentTo(["Work"]);
    }

    [Test]
    public async Task List_ForAnUnknownOwner_IsEmpty()
    {
        var archives = await Bus.InvokeAsync<IReadOnlyList<Archive>>(new FindArchivesByOwnerQuery(Guid.NewGuid()));

        await Assert.That(archives).IsEmpty();
    }

    public async ValueTask DisposeAsync() => await pipeline.DisposeAsync();
}
