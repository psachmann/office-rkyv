using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Documents;

namespace OfficeRkyv.UI.Shared;

/// <summary>
/// Queries are shared: one per circuit, so every component showing the same key shares one cache entry.
/// Mutations are not: a <c>Create…Mutation</c> call returns a fresh instance that the calling component owns
/// and disposes, so its state only ever reports executions that component started.
/// </summary>
public sealed class DocumentsFacade(IQueryClient queryClient, DocumentsService.DocumentsServiceClient documentService)
    : IDisposable
{
    // Any document change can move a document between the inbox and an archive's contents, or change what a
    // search finds, so every document mutation refreshes all three.
    private static readonly string[] DocumentKinds =
    [
        WorkspaceKeys.Inbox,
        WorkspaceKeys.ArchiveDocuments,
        WorkspaceKeys.Search,
    ];

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

    /// <summary>
    /// The owner's documents, from the inbox and every archive, whose title or file name contains every word of the
    /// query, best match first. Not called for a query the server would refuse as too short.
    /// </summary>
    public IQuery<DocumentSearch, DocumentSearchResult> SearchDocuments { get; } =
        queryClient.CreateQuery(
            new QueryOptions<DocumentSearch, DocumentSearchResult>
            {
                KeyFactory = WorkspaceKeys.ForSearch,
                Fetcher = async (search, ct) =>
                {
                    var response = await documentService.SearchDocumentsAsync(
                        new SearchDocumentsRequest { OwnerId = search.OwnerId.ToString(), Query = search.Query },
                        null,
                        null,
                        ct
                    );

                    return new DocumentSearchResult([.. response.Documents], response.HasMore);
                },
            }
        );

    /// <summary>
    /// Uploads one file as a new document and returns it. This is a method and not a mutation because a
    /// mutation has no progress to report: <paramref name="onProgress"/> gets the number of bytes sent so far.
    /// A refusal is thrown as the typed client throws every refusal, so <c>WorkspaceErrors</c> explains it.
    /// </summary>
    /// <remarks>
    /// The server may refuse while the content is still being sent (too large): the next write then fails
    /// because the call is over, and the call's own result says why, so that is what is thrown.
    /// </remarks>
    public async Task<DocumentDto> UploadAsync(
        Guid ownerId,
        string fileName,
        string? contentType,
        Stream content,
        Action<long>? onProgress = null,
        CancellationToken cancellation = default
    )
    {
        using var call = documentService.UploadDocument(cancellationToken: cancellation);

        try
        {
            await call.RequestStream.WriteAsync(
                new UploadDocumentRequest
                {
                    Header = new UploadDocumentHeader
                    {
                        OwnerId = ownerId.ToString(),
                        FileName = fileName,
                        ContentType = contentType ?? "",
                    },
                },
                cancellation
            );

            var buffer = new byte[FileLimits.ChunkSize];
            long sent = 0;
            int read;

            while ((read = await content.ReadAsync(buffer, cancellation)) > 0)
            {
                await call.RequestStream.WriteAsync(
                    new UploadDocumentRequest { Chunk = ByteString.CopyFrom(buffer, 0, read) },
                    cancellation
                );
                sent += read;
                onProgress?.Invoke(sent);
            }

            await call.RequestStream.CompleteAsync();
        }
        catch (Exception exception) when (exception is InvalidOperationException or RpcException)
        {
            // The server ended the call before the content was all sent; its answer is the real error.
        }

        var response = await call.ResponseAsync.WaitAsync(cancellation);
        queryClient.Invalidate(DocumentKinds);

        return response.Document;
    }

    /// <summary>
    /// Opens the document's file for reading. A refusal (unknown or deleted document) is thrown here, before
    /// anything is saved. The caller disposes the stream.
    /// </summary>
    public async Task<Stream> OpenDownloadAsync(DocumentDto document, CancellationToken cancellation = default) =>
        await DownloadStream.OpenAsync(
            documentService.DownloadDocument(
                new DownloadDocumentRequest { DocumentId = document.Id },
                cancellationToken: cancellation
            ),
            document.Size,
            cancellation
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
        SearchDocuments.Dispose();
    }
}
