using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OfficeRkyv.Api.Shared.Auth;
using Wolverine;

namespace OfficeRkyv.Api.Features.Archives;

public class ArchivesServiceImpl(IMessageBus bus, CallerAccessor caller) : ArchivesService.ArchivesServiceBase
{
    public override async Task<ListArchivesResponse> ListArchives(
        ListArchivesRequest request,
        ServerCallContext context
    )
    {
        var callerId = await caller.RequireAsync(context);
        var archives = await bus.InvokeAsync<IReadOnlyList<Archive>>(
            request.ToQuery(callerId),
            context.CancellationToken
        );

        return archives.ToResponse();
    }

    public override async Task<CreateArchiveResponse> CreateArchive(
        CreateArchiveRequest request,
        ServerCallContext context
    )
    {
        var callerId = await caller.RequireAsync(context);
        var archive = await bus.InvokeAsync<Archive>(request.ToCommand(callerId), context.CancellationToken);

        return archive.ToCreateResponse();
    }

    public override async Task<RenameArchiveResponse> RenameArchive(
        RenameArchiveRequest request,
        ServerCallContext context
    )
    {
        var command = request.ToCommand(await caller.RequireAsync(context));
        await bus.InvokeAsync(command, context.CancellationToken);

        // Inline projection: the snapshot is already current, so this reads the result of the write
        // that just happened rather than racing a background projector.
        var archive = await bus.InvokeAsync<Archive?>(
            new FindArchiveByIdQuery(command.ArchiveId),
            context.CancellationToken
        );

        return archive!.ToRenameResponse();
    }

    // Returns once the archive is deleted. Its documents return to the inbox shortly afterwards, from
    // messages Wolverine persisted in the same transaction as the deletion.
    public override async Task<Empty> DeleteArchive(DeleteArchiveRequest request, ServerCallContext context)
    {
        var callerId = await caller.RequireAsync(context);
        await bus.InvokeAsync(request.ToCommand(callerId), context.CancellationToken);

        return new();
    }
}
