using System.Reactive.Disposables;
using Microsoft.AspNetCore.Components;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.UI.Components.Shared;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UI.Components.Shared;

public partial class DocumentDialog
{
    /// <summary>The document to rename.</summary>
    [Parameter, EditorRequired]
    public DocumentDto Document { get; set; } = null!;

    private readonly DocumentModel model = new();
    private readonly CompositeDisposable disposables = [];
    private IMutation<RenameDocumentRequest, DocumentDto> renameDocument = null!;
    private DocumentErrors errors = new();
    private bool isBusy;

    protected override void OnInitialized()
    {
        model.Title = Document.Title;

        // This dialog's own mutation: it starts idle and only reports executions started from here.
        renameDocument = Documents.CreateRenameDocumentMutation();
        disposables.Add(renameDocument);
        disposables.Add(renameDocument.Success.Subscribe(result => _ = InvokeAsync(() => DialogService.Close(true))));
        disposables.Add(renameDocument.Failure.Subscribe(error => _ = InvokeAsync(() => ShowFailure(error))));
    }

    // Only the obvious mistake is stopped here (Radzen's required validator); whether the input is really
    // valid is the server's call and comes back through the mutation state.
    private void Submit()
    {
        errors = new();
        isBusy = true;

        renameDocument.Execute(
            new RenameDocumentRequest
            {
                DocumentId = Document.Id,
                Version = Document.Version,
                Title = model.Title,
            }
        );
    }

    private void ShowFailure(Exception exception)
    {
        errors = DocumentErrors.From(exception);
        isBusy = false;
        _ = DocumentErrors.EndSignInIfRefusedAsync(exception, SignIn);

        // The document changed or is gone: show what is there now, behind the dialog.
        if (errors.ReloadNeeded)
        {
            QueryClient.Invalidate(QueryKinds.Archives, QueryKinds.Inbox, QueryKinds.ArchiveDocuments);
        }

        StateHasChanged();
    }

    public void Dispose() => disposables.Dispose();

    private sealed class DocumentModel
    {
        public string Title { get; set; } = "";
    }
}
