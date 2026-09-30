using System.Net;
using Fisher;
using FluentValidation;
using Google.Rpc;
using Grpc.Core;
using Grpc.Net.Client;
using JasperFx.Events;
using JasperFx.Events.Documents;
using JasperFx.Events.Projections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using OfficeRkyv.Api;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Features.Users.Handlers;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.Api.Shared.Errors;
using OfficeRkyv.Api.Shared.Exceptions;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Users;
using OfficeRkyv.UI.Components.Pages.Users;
using Wolverine;
using Wolverine.Fisher;
using Wolverine.Grpc.Client;

namespace OfficeRkyv.UnitTests.Users;

/// <summary>
/// Drives the real Wolverine pipeline - validation and decider middleware, concurrency guard and all -
/// against a real Fisher store, and serves the real gRPC service with the real error mapping, so the
/// OverTheWire tests see exactly what a client sees.
/// </summary>
public sealed class UserPipelineTests : IAsyncDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.db");
    private WebApplication host = null!;
    private GrpcChannel channel = null!;
    private Uri address = null!;

    private IMessageBus Bus => host.Services.GetRequiredService<IMessageBus>();

    private UsersService.UsersServiceClient Client => new(channel);

    [Before(Test)]
    public async Task SetUp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(kestrel =>
            kestrel.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2)
        );
        builder
            .Services.AddFisher(options =>
            {
                options.Connection($"Data Source={path}");
                options.Projections.Snapshot<User>(SnapshotLifecycle.Inline);
            })
            .ApplyAllDatabaseChangesOnStartup()
            .IntegrateWithWolverine();
        builder.Host.UseWolverine(options =>
        {
            options.ApplicationAssembly = OfficeRkyvApiLib.Assembly;
            options.Durability.Mode = DurabilityMode.Solo;
            options.Policies.AutoApplyTransactions();
            options.UseApiErrorMapping();
        });
        builder.Services.AddGrpc();

        host = builder.Build();
        host.MapGrpcService<UsersServiceImpl>();
        await host.StartAsync();

        var addresses = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        address = new Uri(addresses.Addresses.Single());
        channel = GrpcChannel.ForAddress(address);
    }

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

    // What a gRPC client sees: a real call through the real service and error mapping. These exist
    // because assuming which exception the store throws was wrong once.
    private static async Task<RpcException> FailureOf(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (RpcException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected the call to fail.");
    }

    private static T Detail<T>(RpcException failure)
        where T : Google.Protobuf.IMessage<T>, new() => failure.GetRpcStatus()!.Details.Single().Unpack<T>();

    [Test]
    public async Task OverTheWire_ADuplicateEmail_IsAlreadyExists()
    {
        await Bus.InvokeAsync<User>(new CreateUserCommand("dup@example.com", "First"));

        var failure = await FailureOf(async () =>
            await Client.CreateUserAsync(new CreateUserRequest { Email = "DUP@example.com", DisplayName = "Second" })
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.AlreadyExists);
    }

    [Test]
    public async Task OverTheWire_InvalidInput_IsInvalidArgument_WithAReasonPerViolatedRule()
    {
        var failure = await FailureOf(async () =>
            await Client.CreateUserAsync(
                new CreateUserRequest
                {
                    Email = new string('a', EmailAddress.MaxLength + 1),
                    DisplayName = new string('n', CreateUserValidator.MaxDisplayNameLength + 1),
                }
            )
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert
            .That(Detail<BadRequest>(failure).FieldViolations.Select(v => (v.Field, v.Reason)))
            .IsEquivalentTo([
                ("email", ViolationCodes.TooLong),
                ("email", ViolationCodes.InvalidFormat),
                ("display_name", ViolationCodes.TooLong),
            ]);
    }

    [Test]
    public async Task OverTheWire_EmptyFields_AreOnlyRequired()
    {
        var failure = await FailureOf(async () => await Client.CreateUserAsync(new CreateUserRequest()));

        // The status message is generic: no per-field English text a client might be tempted to show.
        await Assert.That(failure.Status.Detail).DoesNotContain("Email");
        await Assert
            .That(Detail<BadRequest>(failure).FieldViolations.Select(v => v.Description))
            .DoesNotContain(string.Empty);

        await Assert
            .That(Detail<BadRequest>(failure).FieldViolations.Select(v => (v.Field, v.Reason)))
            .IsEquivalentTo([("email", ViolationCodes.Required), ("display_name", ViolationCodes.Required)]);
    }

    [Test]
    public async Task OverTheWire_AStaleVersion_IsAborted()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("edsger@example.com", "Edsger D"));
        await Bus.InvokeAsync(new UpdateUserCommand(created.Id, created.Version, "Edsger Dijkstra"));

        var failure = await FailureOf(async () =>
            await Client.UpdateUserAsync(
                new UpdateUserRequest
                {
                    UserId = created.Id.ToString(),
                    Version = created.Version,
                    DisplayName = "Late",
                }
            )
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.Aborted);
    }

    [Test]
    public async Task OverTheWire_AnUnknownUser_IsNotFound()
    {
        var failure = await FailureOf(async () =>
            await Client.UpdateUserAsync(
                new UpdateUserRequest
                {
                    UserId = Guid.NewGuid().ToString(),
                    Version = 1,
                    DisplayName = "Ghost",
                }
            )
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.NotFound);
    }

    [Test]
    public async Task OverTheWire_RenamingADeletedUser_IsFailedPrecondition_WithAReason()
    {
        var created = await Bus.InvokeAsync<User>(new CreateUserCommand("gone@example.com", "Gone"));
        await Bus.InvokeAsync(new DeleteUserCommand(created.Id, created.Version));
        var deleted = await Bus.InvokeAsync<User?>(new FindUserByIdQuery(created.Id));

        var failure = await FailureOf(async () =>
            await Client.UpdateUserAsync(
                new UpdateUserRequest
                {
                    UserId = created.Id.ToString(),
                    Version = deleted!.Version,
                    DisplayName = "Back",
                }
            )
        );

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.FailedPrecondition);

        var info = Detail<ErrorInfo>(failure);
        await Assert.That(info.Reason).IsEqualTo(InvariantCodes.UserDeleted);
        await Assert.That(info.Domain).IsEqualTo(ErrorDomain.Value);
    }

    [Test]
    public async Task OverTheWire_ThroughTheWebsTypedClient_TheFormGetsItsOwnMessageKeys()
    {
        // Registered as the Web registers it, in a container with no Wolverine runtime - like the Web.
        await using var web = new ServiceCollection()
            .AddLogging()
            .AddWolverineGrpcClient<UsersService.UsersServiceClient>(options => options.Address = address)
            .Services.BuildServiceProvider();
        var client = web.GetRequiredService<UsersService.UsersServiceClient>();

        Exception? failure = null;
        try
        {
            await client.CreateUserAsync(new CreateUserRequest { Email = "not-an-email", DisplayName = "" });
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await Assert
            .That(CreateUserErrors.From(failure!))
            .IsEqualTo(new(Email: "EmailInvalid", DisplayName: "DisplayNameRequired"));
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

    private static IEnumerable<(string Property, string Code)> Violations(ValidationException failure) =>
        failure.Errors.Select(error => (error.PropertyName, error.ErrorCode));

    [Test]
    public async Task Create_WithEmptyEmailAndBlankName_ReportsBothFieldsAndStoresNothing()
    {
        var failure = await CreateExpectingValidationFailure("", "   ");

        await Assert
            .That(Violations(failure))
            .IsEquivalentTo([
                (nameof(CreateUserCommand.Email), ViolationCodes.Required),
                (nameof(CreateUserCommand.DisplayName), ViolationCodes.Required),
            ]);

        var users = await Bus.InvokeAsync<IReadOnlyList<User>>(new FindAllUsersQuery());
        await Assert.That(users).IsEmpty();
    }

    [Test]
    public async Task Create_WithMalformedEmail_ReportsInvalidFormat()
    {
        var failure = await CreateExpectingValidationFailure("alice.example.com", "Alice");

        await Assert
            .That(Violations(failure))
            .IsEquivalentTo([(nameof(CreateUserCommand.Email), ViolationCodes.InvalidFormat)]);
    }

    [Test]
    public async Task Create_WithOverLongValues_ReportsTooLong()
    {
        var email = new string('a', EmailAddress.MaxLength) + "@example.com";
        var displayName = new string('n', CreateUserValidator.MaxDisplayNameLength + 1);

        var failure = await CreateExpectingValidationFailure(email, displayName);

        await Assert.That(Violations(failure)).Contains((nameof(CreateUserCommand.Email), ViolationCodes.TooLong));
        await Assert
            .That(Violations(failure))
            .Contains((nameof(CreateUserCommand.DisplayName), ViolationCodes.TooLong));
    }

    [Test]
    public async Task Create_WithValuesAtTheLengthLimits_IsAccepted()
    {
        // 64 + '@' + 185 chars + ".com" is 254 - exactly the limit; the display name is exactly 100.
        var email = new string('a', 64) + "@" + new string('b', 185) + ".com";
        var displayName = new string('n', CreateUserValidator.MaxDisplayNameLength);

        var created = await Bus.InvokeAsync<User>(new CreateUserCommand(email, displayName));

        await Assert.That(created.Email.Length).IsEqualTo(EmailAddress.MaxLength);
    }

    public async ValueTask DisposeAsync()
    {
        channel.Dispose();
        await host.StopAsync();
        await host.DisposeAsync();
        File.Delete(path);
    }
}
