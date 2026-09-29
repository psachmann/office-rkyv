using Google.Protobuf.WellKnownTypes;
using OfficeRkyv.Contracts.Documents;

namespace OfficeRkyv.Web.Shared;

public sealed class DocumentsFacade : IDisposable
{
    // Any document change can move a document between the inbox and an archive's contents, so every
    // document mutation refreshes both.
    private static readonly string[] DocumentKinds = [WorkspaceKeys.Inbox, WorkspaceKeys.ArchiveDocuments];

    // Filing also moves the archive's version (it appends an event to it), and the next rename or delete of
    // that archive has to send the new one - so the archive list is refreshed too.
    private static readonly string[] FilingKinds = [.. DocumentKinds, WorkspaceKeys.Archives];

    public DocumentsFacade(IQueryClient queryClient, DocumentsService.DocumentsServiceClient documentService)
    {
        FindInbox = queryClient.CreateQuery(
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

        FindArchiveDocuments = queryClient.CreateQuery(
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

        // Live as long as the facade (one per circuit), so their Success/Failure streams replay the last
        // outcome to new subscribers - a component should only react to executions it started.
        CreateDocument = queryClient.CreateMutation(
            new MutationOptions<CreateDocumentRequest, DocumentDto>
            {
                Mutator = async (request, ct) =>
                    (await documentService.CreateDocumentAsync(request, null, null, ct)).Document,
                OnSuccess = (_, _) => queryClient.Invalidate(DocumentKinds),
            }
        );

        RenameDocument = queryClient.CreateMutation(
            new MutationOptions<RenameDocumentRequest, DocumentDto>
            {
                Mutator = async (request, ct) =>
                    (await documentService.RenameDocumentAsync(request, null, null, ct)).Document,
                OnSuccess = (_, _) => queryClient.Invalidate(DocumentKinds),
            }
        );

        DeleteDocument = queryClient.CreateMutation(
            new MutationOptions<DeleteDocumentRequest, Empty>
            {
                Mutator = async (request, ct) => await documentService.DeleteDocumentAsync(request, null, null, ct),
                OnSuccess = (_, _) => queryClient.Invalidate(DocumentKinds),
            }
        );

        FileDocument = queryClient.CreateMutation(
            new MutationOptions<FileDocumentRequest, DocumentDto>
            {
                Mutator = async (request, ct) =>
                    (await documentService.FileDocumentAsync(request, null, null, ct)).Document,
                OnSuccess = (_, _) => queryClient.Invalidate(FilingKinds),
            }
        );

        UnfileDocument = queryClient.CreateMutation(
            new MutationOptions<UnfileDocumentRequest, DocumentDto>
            {
                Mutator = async (request, ct) =>
                    (await documentService.UnfileDocumentAsync(request, null, null, ct)).Document,
                OnSuccess = (_, _) => queryClient.Invalidate(DocumentKinds),
            }
        );
    }

    /// <summary>The owner's unfiled documents, queried by owner id.</summary>
    public IQuery<Guid, IReadOnlyList<DocumentDto>> FindInbox { get; }

    /// <summary>The documents in one archive, queried by archive id.</summary>
    public IQuery<Guid, IReadOnlyList<DocumentDto>> FindArchiveDocuments { get; }

    public IMutation<CreateDocumentRequest, DocumentDto> CreateDocument { get; }

    public IMutation<RenameDocumentRequest, DocumentDto> RenameDocument { get; }

    public IMutation<DeleteDocumentRequest, Empty> DeleteDocument { get; }

    public IMutation<FileDocumentRequest, DocumentDto> FileDocument { get; }

    public IMutation<UnfileDocumentRequest, DocumentDto> UnfileDocument { get; }

    public void Dispose()
    {
        FindInbox.Dispose();
        FindArchiveDocuments.Dispose();
        CreateDocument.Dispose();
        RenameDocument.Dispose();
        DeleteDocument.Dispose();
        FileDocument.Dispose();
        UnfileDocument.Dispose();
    }
}
