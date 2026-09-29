using Google.Protobuf.WellKnownTypes;
using OfficeRkyv.Contracts.Archives;

namespace OfficeRkyv.Web.Shared;

/// <summary>
/// Queries are shared: one per circuit, so every component showing the same key shares one cache entry.
/// Mutations are not: a <c>Create…Mutation</c> call returns a fresh instance that the calling component owns
/// and disposes, so its state only ever reports executions that component started.
/// </summary>
public sealed class ArchivesFacade(IQueryClient queryClient, ArchivesService.ArchivesServiceClient archiveService)
    : IDisposable
{
    /// <summary>
    /// Deleting an archive returns its documents to the inbox asynchronously, so the inbox refetch that follows
    /// the delete can come back before they have arrived. It is refreshed once more after this delay; the
    /// facade's periodic refetch is the backstop.
    /// </summary>
    internal static readonly TimeSpan CascadeSettleDelay = TimeSpan.FromSeconds(1);

    // The delayed refresh belongs to the circuit, not to the mutation: it still runs if the component that
    // deleted the archive is gone by then, and stops only when the circuit ends.
    private readonly CancellationTokenSource disposed = new();

    /// <summary>The owner's archives, queried by owner id.</summary>
    public IQuery<Guid, IReadOnlyList<ArchiveDto>> FindArchives { get; } =
        queryClient.CreateQuery(
            new QueryOptions<Guid, IReadOnlyList<ArchiveDto>>
            {
                KeyFactory = WorkspaceKeys.ForArchives,
                Fetcher = async (ownerId, ct) =>
                    [
                        .. (
                            await archiveService.ListArchivesAsync(
                                new ListArchivesRequest { OwnerId = ownerId.ToString() },
                                null,
                                null,
                                ct
                            )
                        ).Archives,
                    ],
            }
        );

    public IMutation<CreateArchiveRequest, ArchiveDto> CreateCreateArchiveMutation() =>
        queryClient.CreateMutation(
            new MutationOptions<CreateArchiveRequest, ArchiveDto>
            {
                Mutator = async (request, ct) =>
                    (await archiveService.CreateArchiveAsync(request, null, null, ct)).Archive,
                OnSuccess = (_, _) => queryClient.Invalidate(WorkspaceKeys.Archives),
            }
        );

    public IMutation<RenameArchiveRequest, ArchiveDto> CreateRenameArchiveMutation() =>
        queryClient.CreateMutation(
            new MutationOptions<RenameArchiveRequest, ArchiveDto>
            {
                Mutator = async (request, ct) =>
                    (await archiveService.RenameArchiveAsync(request, null, null, ct)).Archive,
                OnSuccess = (_, _) => queryClient.Invalidate(WorkspaceKeys.Archives),
            }
        );

    public IMutation<DeleteArchiveRequest, Empty> CreateDeleteArchiveMutation() =>
        queryClient.CreateMutation(
            new MutationOptions<DeleteArchiveRequest, Empty>
            {
                Mutator = async (request, ct) => await archiveService.DeleteArchiveAsync(request, null, null, ct),
                // The archive's own documents are in the archive-documents queries and are about to be in the inbox.
                OnSuccess = (_, _) =>
                {
                    queryClient.Invalidate(WorkspaceKeys.Archives, WorkspaceKeys.ArchiveDocuments, WorkspaceKeys.Inbox);
                    _ = InvalidateInboxAgainAsync();
                },
            }
        );

    private async Task InvalidateInboxAgainAsync()
    {
        try
        {
            await Task.Delay(CascadeSettleDelay, disposed.Token);
            queryClient.Invalidate(WorkspaceKeys.Inbox);
        }
        catch (OperationCanceledException)
        {
            // The circuit ended first: nothing is left to refresh.
        }
    }

    public void Dispose()
    {
        disposed.Cancel();
        disposed.Dispose();
        FindArchives.Dispose();
    }
}
