using Google.Protobuf.WellKnownTypes;
using OfficeRkyv.Contracts.Archives;

namespace OfficeRkyv.Web.Shared;

public sealed class ArchivesFacade : IDisposable
{
    /// <summary>
    /// Deleting an archive returns its documents to the inbox asynchronously, so the inbox refetch that follows
    /// the delete can come back before they have arrived. It is refreshed once more after this delay; the
    /// facade's periodic refetch is the backstop.
    /// </summary>
    internal static readonly TimeSpan CascadeSettleDelay = TimeSpan.FromSeconds(1);

    private readonly IQueryClient queryClient;
    private readonly CancellationTokenSource disposed = new();

    public ArchivesFacade(IQueryClient queryClient, ArchivesService.ArchivesServiceClient archiveService)
    {
        this.queryClient = queryClient;

        FindArchives = queryClient.CreateQuery(
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

        // Lives as long as the facade (one per circuit), so its Success/Failure streams replay the last
        // outcome to new subscribers - a component should only react to executions it started.
        CreateArchive = queryClient.CreateMutation(
            new MutationOptions<CreateArchiveRequest, ArchiveDto>
            {
                Mutator = async (request, ct) =>
                    (await archiveService.CreateArchiveAsync(request, null, null, ct)).Archive,
                OnSuccess = (_, _) => queryClient.Invalidate(WorkspaceKeys.Archives),
            }
        );

        RenameArchive = queryClient.CreateMutation(
            new MutationOptions<RenameArchiveRequest, ArchiveDto>
            {
                Mutator = async (request, ct) =>
                    (await archiveService.RenameArchiveAsync(request, null, null, ct)).Archive,
                OnSuccess = (_, _) => queryClient.Invalidate(WorkspaceKeys.Archives),
            }
        );

        DeleteArchive = queryClient.CreateMutation(
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
    }

    /// <summary>The owner's archives, queried by owner id.</summary>
    public IQuery<Guid, IReadOnlyList<ArchiveDto>> FindArchives { get; }

    public IMutation<CreateArchiveRequest, ArchiveDto> CreateArchive { get; }

    public IMutation<RenameArchiveRequest, ArchiveDto> RenameArchive { get; }

    public IMutation<DeleteArchiveRequest, Empty> DeleteArchive { get; }

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
        CreateArchive.Dispose();
        RenameArchive.Dispose();
        DeleteArchive.Dispose();
    }
}
