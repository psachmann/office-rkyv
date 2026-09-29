using Fisher.Linq;
using JasperFx.Events;
using OfficeRkyv.Api.Features.Archives;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.Api.Shared.Exceptions;
using OfficeRkyv.Contracts;
using Wolverine;

namespace OfficeRkyv.UnitTests.Archives;

/// <summary>
/// Filing into an archive while that archive is deleted (design D6). Filing moves the archive's version,
/// so whichever of the two commits second is refused; a document must never end up in a deleted archive.
/// </summary>
public sealed class ArchiveDeletionRaceTests : IAsyncDisposable
{
    private const int Rounds = 40;
    private const int MembersPerRound = 8;

    private PipelineHost pipeline = null!;

    private IMessageBus Bus => pipeline.Bus;

    [Before(Test)]
    public async Task SetUp() => pipeline = await PipelineHost.StartAsync();

    private enum Outcome
    {
        Success,
        RefusedAsAborted,
        RefusedAsArchiveDeleted,
    }

    private static async Task<Outcome> Attempt(Func<Task> action)
    {
        try
        {
            await action();

            return Outcome.Success;
        }
        catch (EventStreamUnexpectedMaxEventIdException)
        {
            return Outcome.RefusedAsAborted;
        }
        catch (InvariantViolationException exception) when (exception.Code == InvariantCodes.ArchiveDeleted)
        {
            return Outcome.RefusedAsArchiveDeleted;
        }
    }

    [Test]
    public async Task FilingWhileTheArchiveIsDeleted_NeverLeavesADocumentInTheDeletedArchive()
    {
        var alice = await Bus.InvokeAsync<User>(new CreateUserCommand("alice@example.com", "Alice"));
        var outcomes = new Dictionary<(Outcome Filing, Outcome Deletion), int>();

        for (var round = 0; round < Rounds; round++)
        {
            var archive = await Bus.InvokeAsync<Archive>(new CreateArchiveCommand(alice.Id, $"Round {round}"));

            // Members make the delete's member query take a while, so the filing can land inside it.
            for (var member = 0; member < MembersPerRound; member++)
            {
                var pre = await Bus.InvokeAsync<Document>(new CreateDocumentCommand(alice.Id, $"Member {member}"));
                await Bus.InvokeAsync<Document>(new FileDocumentCommand(pre.Id, pre.Version, archive.Id));
            }

            var racer = await Bus.InvokeAsync<Document>(new CreateDocumentCommand(alice.Id, "Racer"));
            var current = await Load<Archive>(archive.Id);
            using var start = new ManualResetEventSlim();

            var filing = Task.Run(async () =>
            {
                start.Wait();

                return await Attempt(async () =>
                    await Bus.InvokeAsync<Document>(new FileDocumentCommand(racer.Id, racer.Version, archive.Id))
                );
            });
            var deletion = Task.Run(async () =>
            {
                start.Wait();

                return await Attempt(async () =>
                    await pipeline.InvokeAndSettleAsync(new DeleteArchiveCommand(archive.Id, current.Version))
                );
            });
            start.Set();

            var result = (Filing: await filing, Deletion: await deletion);
            outcomes[result] = outcomes.GetValueOrDefault(result) + 1;

            // The invariant, whatever the interleaving: nothing sits in an archive that is deleted.
            var settled = await Load<Archive>(archive.Id);
            var documents = await AllDocuments();
            if (settled.Status is EntityStatus.Deleted)
            {
                await Assert.That(documents.Where(document => document.ArchiveId == archive.Id)).IsEmpty();
                await Assert.That(documents.Single(document => document.Id == racer.Id).ArchiveId).IsNull();
            }
            else
            {
                // The deletion was refused (a stale write), and a refused deletion leaves the archive as it was.
                await Assert.That(result.Deletion).IsNotEqualTo(Outcome.Success);
            }

            // Filed-then-returned, or refused: at least one of the two always commits (they write the same
            // archive stream, so both cannot lose), and a refused filing left the racer in the inbox.
            await Assert.That(result.Filing == Outcome.Success || result.Deletion == Outcome.Success).IsTrue();
            if (result.Filing != Outcome.Success)
            {
                await Assert.That(documents.Single(document => document.Id == racer.Id).ArchiveId).IsNull();
            }
        }

        Console.WriteLine(
            "Race outcomes over "
                + Rounds
                + " rounds: "
                + string.Join(
                    ", ",
                    outcomes.Select(pair => $"filing {pair.Key.Filing} / deletion {pair.Key.Deletion} x{pair.Value}")
                )
        );
    }

    private async Task<T> Load<T>(Guid id)
        where T : class
    {
        await using var session = pipeline.Store.QuerySession();

        return (await session.LoadAsync<T>(id))!;
    }

    private async Task<IReadOnlyList<Document>> AllDocuments()
    {
        await using var session = pipeline.Store.QuerySession();

        return await session.Query<Document>().ToListAsync();
    }

    public async ValueTask DisposeAsync() => await pipeline.DisposeAsync();
}
