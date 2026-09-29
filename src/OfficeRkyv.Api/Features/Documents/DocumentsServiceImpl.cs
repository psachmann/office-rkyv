using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Wolverine;

namespace OfficeRkyv.Api.Features.Documents;

public class DocumentsServiceImpl(IMessageBus bus) : DocumentsService.DocumentsServiceBase
{
    public override async Task<ListInboxResponse> ListInbox(ListInboxRequest request, ServerCallContext context)
    {
        var documents = await bus.InvokeAsync<IReadOnlyList<Document>>(request.ToQuery(), context.CancellationToken);

        return documents.ToInboxResponse();
    }

    public override async Task<ListArchiveDocumentsResponse> ListArchiveDocuments(
        ListArchiveDocumentsRequest request,
        ServerCallContext context
    )
    {
        var documents = await bus.InvokeAsync<IReadOnlyList<Document>>(request.ToQuery(), context.CancellationToken);

        return documents.ToArchiveDocumentsResponse();
    }

    public override async Task<CreateDocumentResponse> CreateDocument(
        CreateDocumentRequest request,
        ServerCallContext context
    )
    {
        var document = await bus.InvokeAsync<Document>(request.ToCommand(), context.CancellationToken);

        return document.ToCreateResponse();
    }

    public override async Task<RenameDocumentResponse> RenameDocument(
        RenameDocumentRequest request,
        ServerCallContext context
    )
    {
        var command = request.ToCommand();
        await bus.InvokeAsync(command, context.CancellationToken);

        return (await ReadAsync(command.DocumentId, context)).ToRenameResponse();
    }

    public override async Task<Empty> DeleteDocument(DeleteDocumentRequest request, ServerCallContext context)
    {
        await bus.InvokeAsync(request.ToCommand(), context.CancellationToken);

        return new();
    }

    public override async Task<FileDocumentResponse> FileDocument(
        FileDocumentRequest request,
        ServerCallContext context
    )
    {
        var document = await bus.InvokeAsync<Document>(request.ToCommand(), context.CancellationToken);

        return document.ToFileResponse();
    }

    public override async Task<UnfileDocumentResponse> UnfileDocument(
        UnfileDocumentRequest request,
        ServerCallContext context
    )
    {
        var command = request.ToCommand();
        await bus.InvokeAsync(command, context.CancellationToken);

        return (await ReadAsync(command.DocumentId, context)).ToUnfileResponse();
    }

    // Inline projection: the snapshot is already current, so this reads the result of the write
    // that just happened rather than racing a background projector.
    private async Task<Document> ReadAsync(Guid documentId, ServerCallContext context) =>
        (await bus.InvokeAsync<Document?>(new FindDocumentByIdQuery(documentId), context.CancellationToken))!;
}
