using OfficeRkyv.Api.Features.Users;
using Wolverine;

namespace OfficeRkyv.Api.Development;

/// <summary>
/// Creates the demo data through the same commands the UI uses, so seeded data is ordinary data: the handlers
/// trim, normalize and check the owner exactly as they do for anyone else.
/// </summary>
/// <remarks>
/// The demo user is the only marker of "already seeded": archives and documents have no unique key. When the
/// user exists nothing is added, so an API restart on the same database neither duplicates the demo data nor
/// brings back what the developer deleted. A failure is logged and never thrown.
/// </remarks>
public sealed partial class DemoDataSeeder(ILogger<DemoDataSeeder> logger, DemoData data, DocumentIngest ingest)
{
    public async Task SeedAsync(IMessageBus bus, CancellationToken cancellation = default)
    {
        try
        {
            var existing = await bus.InvokeAsync<User?>(new FindUserByEmailQuery(data.Email), cancellation);

            if (existing is not null)
            {
                return;
            }

            var user = await bus.InvokeAsync<User>(new CreateUserCommand(data.Email, data.DisplayName), cancellation);
            await bus.InvokeAsync<Archive>(new CreateArchiveCommand(user.Id, data.ArchiveName), cancellation);

            foreach (var document in data.Documents)
            {
                await using var file = File.OpenRead(Path.Combine(DemoData.SampleDirectory, document.FileName));

                // The title is sent explicitly: it is what the demo calls the file, not the file's own name.
                await ingest.IngestAsync(
                    bus,
                    new UploadHeader(user.Id, document.FileName, "application/pdf", document.Title),
                    DocumentIngest.ChunksOf(file, cancellation),
                    cancellation
                );
            }

            LogSeeded(logger, data.Email);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSeedFailed(logger, exception, data.Email);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded demo data for {Email}")]
    private static partial void LogSeeded(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seeding demo data for {Email} failed; the API continues")]
    private static partial void LogSeedFailed(ILogger logger, Exception exception, string email);
}
