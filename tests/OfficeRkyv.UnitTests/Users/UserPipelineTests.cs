using Fisher;
using Grpc.Core;
using JasperFx.Events;
using JasperFx.Events.Documents;
using JasperFx.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OfficeRkyv.Api;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Features.Users.Handlers;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.Api.Shared.Exceptions;
using OfficeRkyv.Api.Shared.Interceptors;
using Wolverine;
using Wolverine.Fisher;

namespace OfficeRkyv.UnitTests.Users;

/// <summary>
/// Drives the real Wolverine pipeline - decider middleware, concurrency guard and all - against a
/// real Fisher store, so what these tests exercise is what the gRPC services call.
/// </summary>
public sealed class UserPipelineTests : IAsyncDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.db");
    private IHost host = null!;

    private IMessageBus Bus => host.Services.GetRequiredService<IMessageBus>();

    [Before(Test)]
    public async Task SetUp() =>
        host = await Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
                services
                    .AddFisher(options =>
                    {
                        options.Connection($"Data Source={path}");
                        options.Projections.Snapshot<User>(SnapshotLifecycle.Inline);
                    })
                    .ApplyAllDatabaseChangesOnStartup()
                    .IntegrateWithWolverine()
            )
            .UseWolverine(options =>
            {
                options.Durability.Mode = DurabilityMode.Solo;
                options.Policies.AutoApplyTransactions();
                options.Discovery.IncludeAssembly(OfficeRkyvApiLib.Assembly);
            })
            .StartAsync();

    [Test]
    public async Task CreateThenQueryImmediately_SeesTheWrite()
    {
        await Bus.InvokeAsync<User>(new CreateUserCommand("ada@example.com", "Ada Lovelace"));

        // No daemon, no polling, no checkpoint wait - the snapshot was written in the same
        // transaction as the event append.
        var user = await Bus.InvokeAsync<User?>(new FindUserByEmailQuery("ada@example.com"));

        await Assert.That(user).IsNotNull();
        await Assert.That(user!.DisplayName).IsEqualTo("Ada Lovelace");
        await Assert.That(user.Version).IsEqualTo(1);
    }

    [Test]
    public async Task FullLifecycle_CreateRenameDelete_LeavesThreeEventsAndASoftDeletedDocument()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("grace@example.com", "Grace H"));

        await Bus.InvokeAsync(new UpdateUserCommand(created.Id, created.Version, "Grace Hopper"));

        var renamed = await Bus.InvokeAsync<User?>(new FindUserByIdQuery(created.Id));
        await Assert.That(renamed!.DisplayName).IsEqualTo("Grace Hopper");

        await Bus.InvokeAsync(new DeleteUserCommand(created.Id, renamed.Version));

        var remaining = await Bus.InvokeAsync<IReadOnlyList<User>>(new FindAllUsersQuery());
        await Assert.That(remaining.Any(user => user.Id == created.Id)).IsFalse();

        await using var session = host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        var document = await session.LoadAsync<User>(created.Id);
        await Assert.That(document).IsNotNull();
        await Assert.That(document!.Status).IsEqualTo(EntityStatus.Deleted);

        var events = await session.Events.FetchStreamAsync(created.Id);
        await Assert
            .That(events.Select(@event => @event.Data.GetType().Name))
            .IsEquivalentTo([nameof(UserCreated), nameof(UserRenamed), nameof(UserDeleted)]);
    }

    [Test]
    public async Task UpdateWithNoActualChange_AppendsNothing()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("alan@example.com", "Alan Turing"));

        await Bus.InvokeAsync(new UpdateUserCommand(created.Id, created.Version, "Alan Turing"));

        await using var session = host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        var events = await session.Events.FetchStreamAsync(created.Id);

        await Assert.That(events.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DuplicateEmail_IsRefusedByTheStore_NotByAHandlerPreCheck()
    {
        await Bus.InvokeAsync<User>(new CreateUserCommand("dup@example.com", "First"));

        // No AnyAsync check anywhere in CreateUserHandler - the natural key is claimed inside the
        // append transaction, so this is a real constraint rather than a check-then-write race.
        await Assert
            .That(async () => await Bus.InvokeAsync<User>(new CreateUserCommand("dup@example.com", "Second")))
            .Throws<DuplicateNaturalKeyException>();
    }

    [Test]
    public async Task StaleExpectedVersion_IsRejected()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("edsger@example.com", "Edsger D"));

        await Bus.InvokeAsync(new UpdateUserCommand(created.Id, created.Version, "Edsger Dijkstra"));

        // Version 1 is stale now - the rename moved the stream to 2. Optimistic concurrency reaches
        // all the way out to the caller because the command carries the version it read.
        await Assert
            .That(async () => await Bus.InvokeAsync(new UpdateUserCommand(created.Id, created.Version, "Someone Else")))
            .Throws<EventStreamUnexpectedMaxEventIdException>();
    }

    [Test]
    [Arguments("DUP@Example.com")]
    [Arguments("  dup@example.com ")]
    public async Task DuplicateEmail_DifferingOnlyInCaseOrWhitespace_IsRefused(string second)
    {
        await Bus.InvokeAsync<User>(new CreateUserCommand("dup@example.com", "First"));

        await Assert
            .That(async () => await Bus.InvokeAsync<User>(new CreateUserCommand(second, "Second")))
            .Throws<DuplicateNaturalKeyException>();

        var users = await Bus.InvokeAsync<IReadOnlyList<User>>(new FindAllUsersQuery());
        await Assert.That(users.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DifferentEmails_AreBothAccepted()
    {
        await Bus.InvokeAsync<User>(new CreateUserCommand("alice@example.com", "Alice"));
        await Bus.InvokeAsync<User>(new CreateUserCommand("bob@example.com", "Bob"));

        var users = await Bus.InvokeAsync<IReadOnlyList<User>>(new FindAllUsersQuery());
        await Assert.That(users.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Create_KeepsTheEmailAsEntered_AndTrimsTheDisplayName()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("  Alice@Example.com ", "  Alice  "));

        await Assert.That(created.Email).IsEqualTo("Alice@Example.com");
        await Assert.That(created.NormalizedEmail).IsEqualTo("alice@example.com");
        await Assert.That(created.DisplayName).IsEqualTo("Alice");

        // The snapshot read back later carries the same values - they were recorded, not recomputed.
        var listed = await Bus.InvokeAsync<IReadOnlyList<User>>(new FindAllUsersQuery());
        await Assert.That(listed.Single().Email).IsEqualTo("Alice@Example.com");
    }

    [Test]
    [Arguments("alice@example.com")]
    [Arguments("ALICE@EXAMPLE.COM")]
    [Arguments("  Alice@Example.com\t")]
    public async Task FindByEmail_IgnoresCaseAndSurroundingWhitespace(string lookup)
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("Alice@Example.com", "Alice"));

        var found = await Bus.InvokeAsync<User?>(new FindUserByEmailQuery(lookup));

        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Id).IsEqualTo(created.Id);
    }

    [Test]
    public async Task FindByEmail_ForAnUnknownEmail_ReturnsNull()
    {
        await Bus.InvokeAsync<User>(new CreateUserCommand("alice@example.com", "Alice"));

        var found = await Bus.InvokeAsync<User?>(new FindUserByEmailQuery("nobody@example.com"));

        await Assert.That(found).IsNull();
    }

    [Test]
    public async Task FindByEmail_DoesNotReturnADeletedUser()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("gone@example.com", "Gone"));
        await Bus.InvokeAsync(new DeleteUserCommand(created.Id, created.Version));

        var found = await Bus.InvokeAsync<User?>(new FindUserByEmailQuery("gone@example.com"));

        await Assert.That(found).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task UpdateOfAnUnknownUser_IsNotFound_WhateverVersionTheCallerSends(int version)
    {
        // Version 1 would otherwise surface as an unexpected-version conflict (indistinguishable from a
        // stale write) and version 0 as a null user; the handler's Validate step runs before both.
        await Assert
            .That(async () => await Bus.InvokeAsync(new UpdateUserCommand(Guid.NewGuid(), version, "Ghost")))
            .Throws<NotFoundException>();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task DeleteOfAnUnknownUser_IsNotFound_WhateverVersionTheCallerSends(int version)
    {
        await Assert
            .That(async () => await Bus.InvokeAsync(new DeleteUserCommand(Guid.NewGuid(), version)))
            .Throws<NotFoundException>();
    }

    [Test]
    public async Task RenamingADeletedUser_ViolatesAnInvariant_NotNotFound()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("gone@example.com", "Gone"));
        await Bus.InvokeAsync(new DeleteUserCommand(created.Id, created.Version));

        // The soft-deleted user still exists, so the existence check passes and the decider refuses.
        var deleted = await Bus.InvokeAsync<User?>(new FindUserByIdQuery(created.Id));

        await Assert
            .That(async () => await Bus.InvokeAsync(new UpdateUserCommand(created.Id, deleted!.Version, "Back")))
            .Throws<InvariantViolationException>();
    }

    // What a gRPC client would see: the pipeline's real exception, run through the real mapping. These
    // exist because assuming which exception the store throws was wrong once (see design D3).
    private static async Task<RpcException> MappedFailureOf(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (Exception exception)
        {
            return ExceptionMappingInterceptor.Map(exception);
        }

        throw new InvalidOperationException("Expected the call to fail.");
    }

    [Test]
    public async Task OverTheWire_ADuplicateEmail_IsAlreadyExists()
    {
        await Bus.InvokeAsync<User>(new CreateUserCommand("dup@example.com", "First"));

        var mapped = await MappedFailureOf(async () =>
            await Bus.InvokeAsync<User>(new CreateUserCommand("DUP@example.com", "Second"))
        );

        await Assert.That(mapped.StatusCode).IsEqualTo(StatusCode.AlreadyExists);
    }

    [Test]
    public async Task OverTheWire_InvalidInput_IsInvalidArgument()
    {
        var mapped = await MappedFailureOf(async () => await Bus.InvokeAsync<User>(new CreateUserCommand("", "")));

        await Assert.That(mapped.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
    }

    [Test]
    public async Task OverTheWire_AStaleVersion_IsAborted()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("edsger@example.com", "Edsger D"));
        await Bus.InvokeAsync(new UpdateUserCommand(created.Id, created.Version, "Edsger Dijkstra"));

        var mapped = await MappedFailureOf(async () =>
            await Bus.InvokeAsync(new UpdateUserCommand(created.Id, created.Version, "Late"))
        );

        await Assert.That(mapped.StatusCode).IsEqualTo(StatusCode.Aborted);
    }

    [Test]
    public async Task OverTheWire_AnUnknownUser_IsNotFound()
    {
        var mapped = await MappedFailureOf(async () =>
            await Bus.InvokeAsync(new UpdateUserCommand(Guid.NewGuid(), 1, "Ghost"))
        );

        await Assert.That(mapped.StatusCode).IsEqualTo(StatusCode.NotFound);
    }

    [Test]
    public async Task OverTheWire_RenamingADeletedUser_IsFailedPrecondition()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("gone@example.com", "Gone"));
        await Bus.InvokeAsync(new DeleteUserCommand(created.Id, created.Version));
        var deleted = await Bus.InvokeAsync<User?>(new FindUserByIdQuery(created.Id));

        var mapped = await MappedFailureOf(async () =>
            await Bus.InvokeAsync(new UpdateUserCommand(created.Id, deleted!.Version, "Back"))
        );

        await Assert.That(mapped.StatusCode).IsEqualTo(StatusCode.FailedPrecondition);
    }

    private async Task<ValidationException> CreateExpectingValidationFailure(string email, string displayName)
    {
        try
        {
            await Bus.InvokeAsync<User>(new CreateUserCommand(email, displayName));
        }
        catch (ValidationException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected the create to be rejected as invalid.");
    }

    [Test]
    public async Task Create_WithEmptyEmailAndBlankName_ReportsBothFieldsAndStoresNothing()
    {
        var failure = await CreateExpectingValidationFailure("", "   ");

        await Assert
            .That(failure.Violations)
            .IsEquivalentTo([
                new FieldViolation(nameof(CreateUserCommand.Email), ViolationCodes.Required),
                new FieldViolation(nameof(CreateUserCommand.DisplayName), ViolationCodes.Required),
            ]);

        var users = await Bus.InvokeAsync<IReadOnlyList<User>>(new FindAllUsersQuery());
        await Assert.That(users).IsEmpty();
    }

    [Test]
    public async Task Create_WithMalformedEmail_ReportsInvalidFormat()
    {
        var failure = await CreateExpectingValidationFailure("alice.example.com", "Alice");

        await Assert
            .That(failure.Violations)
            .IsEquivalentTo([new FieldViolation(nameof(CreateUserCommand.Email), ViolationCodes.InvalidFormat)]);
    }

    [Test]
    public async Task Create_WithOverLongValues_ReportsTooLong()
    {
        var email = new string('a', EmailAddress.MaxLength) + "@example.com";
        var displayName = new string('n', CreateUserHandler.MaxDisplayNameLength + 1);

        var failure = await CreateExpectingValidationFailure(email, displayName);

        await Assert.That(failure.Violations).Contains(new FieldViolation("Email", ViolationCodes.TooLong));
        await Assert.That(failure.Violations).Contains(new FieldViolation("DisplayName", ViolationCodes.TooLong));
    }

    [Test]
    public async Task Create_WithValuesAtTheLengthLimits_IsAccepted()
    {
        // 64 + '@' + 185 chars + ".com" is 254 - exactly the limit; the display name is exactly 100.
        var email = new string('a', 64) + "@" + new string('b', 185) + ".com";
        var displayName = new string('n', CreateUserHandler.MaxDisplayNameLength);

        var created = await Bus.InvokeAsync<User>(new CreateUserCommand(email, displayName));

        await Assert.That(created.Email.Length).IsEqualTo(EmailAddress.MaxLength);
    }

    public async ValueTask DisposeAsync()
    {
        await host.StopAsync();
        host.Dispose();
        File.Delete(path);
    }
}
