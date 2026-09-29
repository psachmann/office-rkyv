using Google.Protobuf.WellKnownTypes;
using OfficeRkyv.Contracts.Documents;

namespace OfficeRkyv.Web.Shared;

/// <summary>
/// Queries are shared: one per circuit, so every component showing the same key shares one cache entry.
/// Mutations are not: a <c>Create…Mutation</c> call returns a fresh instance that the calling component owns
/// and disposes, so its state only ever reports executions that component started.
/// </summary>
public sealed class DocumentsFacade(IQueryClient queryClient, DocumentsService.DocumentsServiceClient documentService)
    : IDisposable
{
    // Any document change can move a document between the inbox and an archive's contents, so every
    // document mutation refreshes both.
    private static readonly string[] DocumentKinds = [WorkspaceKeys.Inbox, WorkspaceKeys.ArchiveDocuments];

    // Filing also moves the archive's version (it appends an event to it), and the next rename or delete of
    // that archive has to send the new one - so the archive list is refreshed too.
    private static readonly string[] FilingKinds = [.. DocumentKinds, WorkspaceKeys.Archives];

    /// <summary>The owner's unfiled documents, queried by owner id.</summary>
    public IQuery<Guid, IReadOnlyList<DocumentDto>> FindInbox { get; } =
        queryClient.CreateQuery(
            new QueryOptions<Guid, IReadOnlyList<DocumentDto>>
            {
                KeyFactory = WorkspaceKeys.ForInbox,
                Fetcher = async (ownerId, ct) =>
                    [
                        .. (
                            await documentService.ListInboxAsync(
                                new ListInboxRequest { OwnerId = ownerId.ToString() },
                                null,
                                null,
                                ct
                            )
                        ).Documents,
                    ],
            }
        );

    /// <summary>The documents in one archive, queried by archive id.</summary>
    public IQuery<Guid, IReadOnlyList<DocumentDto>> FindArchiveDocuments { get; } =
        queryClient.CreateQuery(
            new QueryOptions<Guid, IReadOnlyList<DocumentDto>>
            {
                KeyFactory = WorkspaceKeys.ForArchiveDocuments,
                Fetcher = async (archiveId, ct) =>
                    [
                        .. (
                            await documentService.ListArchiveDocumentsAsync(
                                new ListArchiveDocumentsRequest { ArchiveId = archiveId.ToString() },
                                null,
                                null,
                                ct
                            )
                        ).Documents,
                    ],
            }
        );

    public IMutation<CreateDocumentRequest, DocumentDto> CreateCreateDocumentMutation() =>
        queryClient.CreateMutation(
            new MutationOptions<CreateDocumentRequest, DocumentDto>
            {
                Mutator = async (request, ct) =>
                    (await documentService.CreateDocumentAsync(request, null, null, ct)).Document,
                OnSuccess = (_, _) => queryClient.Invalidate(DocumentKinds),
            }
        );

    public IMutation<RenameDocumentRequest, DocumentDto> CreateRenameDocumentMutation() =>
        queryClient.CreateMutation(
            new MutationOptions<RenameDocumentRequest, DocumentDto>
            {
                Mutator = async (request, ct) =>
                    (await documentService.RenameDocumentAsync(request, null, null, ct)).Document,
                OnSuccess = (_, _) => queryClient.Invalidate(DocumentKinds),
            }
        );

    public IMutation<DeleteDocumentRequest, Empty> CreateDeleteDocumentMutation() =>
        queryClient.CreateMutation(
            new MutationOptions<DeleteDocumentRequest, Empty>
            {
                Mutator = async (request, ct) => await documentService.DeleteDocumentAsync(request, null, null, ct),
                OnSuccess = (_, _) => queryClient.Invalidate(DocumentKinds),
            }
        );

    public IMutation<FileDocumentRequest, DocumentDto> CreateFileDocumentMutation() =>
        queryClient.CreateMutation(
            new MutationOptions<FileDocumentRequest, DocumentDto>
            {
                Mutator = async (request, ct) =>
                    (await documentService.FileDocumentAsync(request, null, null, ct)).Document,
                OnSuccess = (_, _) => queryClient.Invalidate(FilingKinds),
            }
        );

    public IMutation<UnfileDocumentRequest, DocumentDto> CreateUnfileDocumentMutation() =>
        queryClient.CreateMutation(
            new MutationOptions<UnfileDocumentRequest, DocumentDto>
            {
                Mutator = async (request, ct) =>
                    (await documentService.UnfileDocumentAsync(request, null, null, ct)).Document,
                OnSuccess = (_, _) => queryClient.Invalidate(DocumentKinds),
            }
        );

    public void Dispose()
    {
        FindInbox.Dispose();
        FindArchiveDocuments.Dispose();
    }
}
