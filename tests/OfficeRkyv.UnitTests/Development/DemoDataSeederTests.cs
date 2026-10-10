using Microsoft.Extensions.Logging;
using OfficeRkyv.Api.Development;
using OfficeRkyv.Api.Features.Archives;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Api.Features.Users;
using Wolverine;

namespace OfficeRkyv.UnitTests.Development;

/// <summary>
/// The development seed against the real pipeline and a real store, read back through the same queries the UI's
/// facades end up in, so "ordinary data" is checked from the outside.
/// </summary>
public sealed class DemoDataSeederTests : IAsyncDisposable
{
    private PipelineHost pipeline = null!;

    private IMessageBus Bus => pipeline.Bus;

    [Before(Test)]
    public async Task SetUp() => pipeline = await PipelineHost.StartAsync();

    private static DemoDataSeeder SeederWith(CapturingLogger logger, DemoData? data = null) =>
        new(logger, data ?? DemoData.Default);

    private async Task<IReadOnlyList<User>> Users() =>
        await Bus.InvokeAsync<IReadOnlyList<User>>(new FindAllUsersQuery());

    private async Task<IReadOnlyList<Archive>> Archives(User owner) =>
        await Bus.InvokeAsync<IReadOnlyList<Archive>>(new FindArchivesByOwnerQuery(owner.Id));

    private async Task<IReadOnlyList<Document>> Inbox(User owner) =>
        await Bus.InvokeAsync<IReadOnlyList<Document>>(new FindInboxQuery(owner.Id));

    private async Task<IReadOnlyList<Document>> Contents(Archive archive) =>
        await Bus.InvokeAsync<IReadOnlyList<Document>>(new FindArchiveDocumentsQuery(archive.Id));

    [Test]
    public async Task AnEmptyDatabase_GetsTheDemoUser_ItsEmptyArchive_AndThreeInboxDocuments()
    {
        var logger = new CapturingLogger();

        await SeederWith(logger).SeedAsync(Bus);

        var user = (await Users()).Single();
        await Assert.That(user.Email).IsEqualTo("demo@example.com");
        await Assert.That(user.DisplayName).IsEqualTo("Demo User");

        var archive = (await Archives(user)).Single();
        await Assert.That(archive.Name).IsEqualTo("Taxes 2026");
        await Assert.That(await Contents(archive)).IsEmpty();

        await Assert
            .That((await Inbox(user)).Select(document => document.Title).Order())
            .IsEquivalentTo(["Electricity bill March", "Insurance policy", "Rental contract"]);

        await Assert.That(logger.Entries.Select(entry => entry.Level)).IsEquivalentTo([LogLevel.Information]);
    }

    [Test]
    public async Task BesideAnotherUser_TheDemoDataIsAdded_AndTheOtherUserIsUntouched()
    {
        var alice = await Bus.InvokeAsync<User>(new CreateUserCommand("alice@example.com", "Alice"));

        await SeederWith(new CapturingLogger()).SeedAsync(Bus);

        var users = await Users();
        await Assert.That(users.Select(user => user.Email)).IsEquivalentTo(["alice@example.com", "demo@example.com"]);
        await Assert.That(users.Single(user => user.Id == alice.Id).Version).IsEqualTo(alice.Version);
        await Assert.That(await Archives(alice)).IsEmpty();
        await Assert.That(await Inbox(alice)).IsEmpty();
    }

    [Test]
    public async Task SeedingTwice_AddsNothingTheSecondTime()
    {
        var logger = new CapturingLogger();
        var seeder = SeederWith(logger);

        await seeder.SeedAsync(Bus);
        await seeder.SeedAsync(Bus);

        var user = (await Users()).Single();
        await Assert.That(await Archives(user)).Count().IsEqualTo(1);
        await Assert.That(await Inbox(user)).Count().IsEqualTo(3);

        // Only the run that seeded says so.
        await Assert.That(logger.Entries).Count().IsEqualTo(1);
    }

    [Test]
    public async Task DataTheDeveloperDeleted_IsNotCreatedAgain()
    {
        var seeder = SeederWith(new CapturingLogger());
        await seeder.SeedAsync(Bus);

        var user = (await Users()).Single();
        var archive = (await Archives(user)).Single();
        var document = (await Inbox(user)).First();
        await Bus.InvokeAsync(new DeleteArchiveCommand(archive.Id, archive.Version));
        await Bus.InvokeAsync(new DeleteDocumentCommand(document.Id, document.Version));

        await seeder.SeedAsync(Bus);

        await Assert.That(await Users()).Count().IsEqualTo(1);
        await Assert.That(await Archives(user)).IsEmpty();
        await Assert.That(await Inbox(user)).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ASeededDocument_CanBeFiledIntoTheSeededArchive_LikeAnyOther()
    {
        await SeederWith(new CapturingLogger()).SeedAsync(Bus);
        var user = (await Users()).Single();
        var archive = (await Archives(user)).Single();
        var contract = (await Inbox(user)).Single(document => document.Title == "Rental contract");

        await Bus.InvokeAsync<Document>(new FileDocumentCommand(contract.Id, contract.Version, archive.Id));

        await Assert
            .That((await Contents(archive)).Select(document => document.Title))
            .IsEquivalentTo(["Rental contract"]);
        await Assert.That(await Inbox(user)).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ARefusedFirstCommand_EndsTheSeedQuietly_WithOneWarning_AndNothingElseCreated()
    {
        var logger = new CapturingLogger();
        var refused = DemoData.Default with { Email = "not-an-email" };

        await SeederWith(logger, refused).SeedAsync(Bus);

        await Assert.That(await Users()).IsEmpty();
        var entry = logger.Entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Exception).IsNotNull();
    }

    [Test]
    public async Task TheSharedComposition_DoesNotSeed_EvenInDevelopment()
    {
        await using var development = await PipelineHost.StartAsync("Development");

        var users = await development.Bus.InvokeAsync<IReadOnlyList<User>>(new FindAllUsersQuery());

        await Assert.That(users).IsEmpty();
    }

    [Test]
    public async Task TheApiServersSeed_RunsAfterStartup_InDevelopment()
    {
        await using var development = await PipelineHost.StartAsync(
            "Development",
            builder => builder.AddDevelopmentSeedData()
        );

        var users = await WaitForUsers(development);

        await Assert.That(users.Select(user => user.Email)).IsEquivalentTo(["demo@example.com"]);
    }

    [Test]
    [Arguments("Production", null, false)]
    [Arguments("Production", "true", false)]
    [Arguments("Development", "false", false)]
    [Arguments("Development", "true", true)]
    [Arguments("Development", "not-a-boolean", true)]
    public async Task TheSeedIsRegistered_OnlyWhenTheEnvironmentAndTheSettingAllowIt(
        string environment,
        string? setting,
        bool expected
    )
    {
        await using var host = await PipelineHost.StartAsync(
            environment,
            builder =>
            {
                builder.Configuration[DemoDataSeeding.SettingName] = setting;
                builder.AddDevelopmentSeedData();
            }
        );

        await Assert.That(host.Host.Services.GetService<DemoDataSeeder>() is not null).IsEqualTo(expected);
    }

    private static async Task<IReadOnlyList<User>> WaitForUsers(PipelineHost host)
    {
        // The seed runs in the background after startup, so poll instead of assuming it is done.
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (true)
        {
            var users = await host.Bus.InvokeAsync<IReadOnlyList<User>>(new FindAllUsersQuery());

            if (users.Count > 0 || DateTime.UtcNow > deadline)
            {
                return users;
            }

            await Task.Delay(100);
        }
    }

    public async ValueTask DisposeAsync() => await pipeline.DisposeAsync();

    private sealed class CapturingLogger : ILogger<DemoDataSeeder>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add((logLevel, exception));
    }
}
