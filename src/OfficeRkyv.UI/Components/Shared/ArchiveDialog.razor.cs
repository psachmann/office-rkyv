using System.Reactive.Disposables;
using Microsoft.AspNetCore.Components;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.UI.Components.Shared;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UI.Components.Shared;

public partial class ArchiveDialog
{
    /// <summary>The archive to rename. Null creates a new one, for the signed-in user.</summary>
    [Parameter]
    public ArchiveDto? Archive { get; set; }

    private readonly ArchiveModel model = new();
    private readonly CompositeDisposable disposables = [];
    private IMutation<CreateArchiveRequest, ArchiveDto>? createArchive;
    private IMutation<RenameArchiveRequest, ArchiveDto>? renameArchive;
    private DocumentErrors errors = new();
    private bool isBusy;

    private bool IsRename => Archive is not null;

    protected override void OnInitialized()
    {
        model.Name = Archive?.Name ?? "";

        // This dialog's own mutation: it starts idle and only reports executions started from here.
        if (Archive is null)
        {
            createArchive = Archives.CreateCreateArchiveMutation();
            Track(createArchive);
        }
        else
        {
            renameArchive = Archives.CreateRenameArchiveMutation();
            Track(renameArchive);
        }
    }

    private void Track<TArgs, TData>(IMutation<TArgs, TData> mutation)
    {
        disposables.Add(mutation);
        disposables.Add(mutation.Success.Subscribe(result => _ = InvokeAsync(() => DialogService.Close(true))));
        disposables.Add(mutation.Failure.Subscribe(error => _ = InvokeAsync(() => ShowFailure(error))));
    }

    // Only the obvious mistake is stopped here (Radzen's required validator); whether the input is really
    // valid is the server's call and comes back through the mutation state.
    private void Submit()
    {
        errors = new();
        isBusy = true;

        if (Archive is { } archive)
        {
            renameArchive!.Execute(
                new RenameArchiveRequest
                {
                    ArchiveId = archive.Id,
                    Version = archive.Version,
                    Name = model.Name,
                }
            );
        }
        else
        {
            createArchive!.Execute(new CreateArchiveRequest { Name = model.Name });
        }
    }

    private void ShowFailure(Exception exception)
    {
        errors = DocumentErrors.From(exception);
        isBusy = false;
        _ = DocumentErrors.EndSignInIfRefusedAsync(exception, SignIn);

        // The archive changed or is gone: show what is there now, behind the dialog.
        if (errors.ReloadNeeded)
        {
            QueryClient.Invalidate(QueryKinds.Archives, QueryKinds.Inbox, QueryKinds.ArchiveDocuments);
        }

        StateHasChanged();
    }

    public void Dispose() => disposables.Dispose();

    private sealed class ArchiveModel
    {
        public string Name { get; set; } = "";
    }
}
