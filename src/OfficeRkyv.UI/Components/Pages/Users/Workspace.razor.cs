using System.Reactive.Disposables;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.UI.Auth;
using OfficeRkyv.UI.Shared;
using Radzen;

namespace OfficeRkyv.UI.Components.Pages.Users;

public partial class Workspace
{
    // The value the "File into..." menu uses for the inbox; archive entries carry their archive id.
    private const string InboxMenuValue = "inbox";

    private readonly CompositeDisposable disposables = [];
    private IMutation<FileDocumentRequest, DocumentDto> fileDocument = null!;
    private IMutation<UnfileDocumentRequest, DocumentDto> unfileDocument = null!;
    private IMutation<DeleteDocumentRequest, Google.Protobuf.WellKnownTypes.Empty> deleteDocument = null!;
    private IMutation<DeleteArchiveRequest, Google.Protobuf.WellKnownTypes.Empty> deleteArchive = null!;
    private IReadOnlyList<ArchiveDto> archiveSnapshot = [];
    private string? errorKey;

    [CascadingParameter]
    private Task<AuthenticationState> AuthenticationState { get; set; } = null!;

    /// <summary>
    /// The signed-in user, from the authentication state: a user's id is never part of the address. The page is
    /// only reachable signed in, so this is empty only for the moment a sign-out is taking effect.
    /// </summary>
    private Guid UserId { get; set; }

    /// <summary>The selected archive; null selects the inbox.</summary>
    [Parameter]
    public Guid? ArchiveId { get; set; }

    private ArchiveDto? SelectedArchive(IReadOnlyList<ArchiveDto> archives) =>
        ArchiveId is { } id ? archives.FirstOrDefault(archive => archive.Id == id.ToString()) : null;

    protected override void OnInitialized()
    {
        // The page's own mutations: they start idle and only report executions started from here.
        fileDocument = Track(Documents.CreateFileDocumentMutation(), ClearError);
        unfileDocument = Track(Documents.CreateUnfileDocumentMutation(), ClearError);
        deleteDocument = Track(Documents.CreateDeleteDocumentMutation(), ClearError);
        deleteArchive = Track(Archives.CreateDeleteArchiveMutation(), OnArchiveDeleted);

        // The list of archives decides whether the selected one still exists: it may have been deleted
        // somewhere else, and the page then falls back to the inbox instead of showing a phantom archive.
        disposables.Add(
            Archives.FindArchives.Success.Subscribe(archives => _ = InvokeAsync(() => OnArchivesLoaded(archives)))
        );

        // A user the API does not know, or does not accept, is no longer signed in. A refused query ends the
        // sign-in as a refused mutation does (ShowFailure).
        disposables.Add(
            Users.FindUserById.Success.Subscribe(found =>
            {
                if (found.User is null)
                {
                    _ = InvokeAsync(SignIn.SignOutAsync);
                }
            })
        );
        WatchForSignOut(Users.FindUserById);
        WatchForSignOut(Archives.FindArchives);
        WatchForSignOut(Documents.FindInbox);
        WatchForSignOut(Documents.FindArchiveDocuments);
    }

    private void WatchForSignOut<TArgs, TData>(IQuery<TArgs, TData> query)
        where TData : class =>
        disposables.Add(
            query.Failure.Subscribe(error =>
                _ = InvokeAsync(() => WorkspaceErrors.EndSignInIfRefusedAsync(error, SignIn))
            )
        );

    protected override async Task OnParametersSetAsync()
    {
        if (SignedInUser.IdOf((await AuthenticationState).User) is not { } userId)
        {
            return;
        }

        UserId = userId;

        Users.FindUserById.SetArgs(UserId);
        Archives.FindArchives.SetArgs(UserId);
        Documents.FindInbox.SetArgs(UserId);

        if (ArchiveId is { } archiveId)
        {
            Documents.FindArchiveDocuments.SetArgs(archiveId);
        }
    }

    // Disposed with the page, together with the subscriptions to it.
    private IMutation<TArgs, TData> Track<TArgs, TData>(IMutation<TArgs, TData> mutation, Action onSuccess)
    {
        disposables.Add(mutation);
        disposables.Add(mutation.Success.Subscribe(result => _ = InvokeAsync(onSuccess)));
        disposables.Add(mutation.Failure.Subscribe(error => _ = InvokeAsync(() => ShowFailure(error))));

        return mutation;
    }

    private void ClearError()
    {
        errorKey = null;
        StateHasChanged();
    }

    private void OnArchivesLoaded(IReadOnlyList<ArchiveDto> archives)
    {
        archiveSnapshot = archives;

        if (ArchiveId is { } id && archives.All(archive => archive.Id != id.ToString()))
        {
            Navigation.NavigateTo(WorkspaceAddresses.Inbox);
        }

        StateHasChanged();
    }

    private void OnArchiveDeleted()
    {
        errorKey = null;

        // Only the selected archive can be deleted, so it was the one being shown.
        if (ArchiveId is not null)
        {
            Navigation.NavigateTo(WorkspaceAddresses.Inbox);
        }

        StateHasChanged();
    }

    private void ShowFailure(Exception exception)
    {
        var errors = WorkspaceErrors.From(exception);
        errorKey = errors.General ?? "ActionFailed";
        _ = WorkspaceErrors.EndSignInIfRefusedAsync(exception, SignIn);

        // The item changed or is gone: what is shown is out of date, so load it again.
        if (errors.ReloadNeeded)
        {
            QueryClient.Invalidate(
                WorkspaceKeys.Archives,
                WorkspaceKeys.Inbox,
                WorkspaceKeys.ArchiveDocuments,
                WorkspaceKeys.Search
            );
        }

        StateHasChanged();
    }

    private Task OpenCreateDocumentDialog() =>
        DialogService.OpenAsync<UploadDocumentsDialog>(
            Localizer["NewDocument"],
            options: new DialogOptions { Width = "28rem" }
        );

    private Task OpenRenameDocumentDialog(DocumentDto document) =>
        DialogService.OpenAsync<DocumentDialog>(
            Localizer["RenameDocument"],
            new Dictionary<string, object?> { [nameof(DocumentDialog.Document)] = document },
            new DialogOptions { Width = "28rem" }
        );

    private Task OpenCreateArchiveDialog() =>
        DialogService.OpenAsync<ArchiveDialog>(Localizer["NewArchive"], options: new DialogOptions { Width = "28rem" });

    private Task OpenRenameArchiveDialog(ArchiveDto archive) =>
        DialogService.OpenAsync<ArchiveDialog>(
            Localizer["RenameArchive"],
            new Dictionary<string, object?> { [nameof(ArchiveDialog.Archive)] = archive },
            new DialogOptions { Width = "28rem" }
        );

    // The other archives, plus the inbox when the document is filed. Choosing one files or unfiles it.
    private void OpenFileMenu(MouseEventArgs args, DocumentDto document)
    {
        var items = archiveSnapshot
            .Where(archive => !document.HasArchiveId || archive.Id != document.ArchiveId)
            .Select(archive => new ContextMenuItem { Text = archive.Name, Value = archive.Id })
            .ToList();

        if (document.HasArchiveId)
        {
            items.Insert(0, new ContextMenuItem { Text = Localizer["FileIntoInbox"], Value = InboxMenuValue });
        }

        if (items.Count == 0)
        {
            items.Add(new ContextMenuItem { Text = Localizer["NoOtherArchives"], Disabled = true });
        }

        ContextMenuService.Open(args, items, choice => FileInto(document, choice.Value?.ToString()));
    }

    private void FileInto(DocumentDto document, string? target)
    {
        ContextMenuService.Close();
        errorKey = null;

        if (target is null)
        {
            return;
        }

        if (target == InboxMenuValue)
        {
            unfileDocument.Execute(new UnfileDocumentRequest { DocumentId = document.Id, Version = document.Version });
        }
        else
        {
            fileDocument.Execute(
                new FileDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    ArchiveId = target,
                }
            );
        }
    }

    // The refusal (unknown or deleted document) is thrown by opening the stream, so it is shown like every other
    // refusal on this page and nothing has been handed to the host yet.
    private async Task DownloadDocument(DocumentDto document)
    {
        errorKey = null;

        try
        {
            await using var content = await Documents.OpenDownloadAsync(document);
            var saved = await FileSaver.SaveAsync(document.FileName, document.ContentType, content);

            // The host says where the file went when it can: the desktop does, a browser has its own list.
            if (saved.Location is { } location)
            {
                NotificationService.Notify(
                    new NotificationMessage
                    {
                        Severity = NotificationSeverity.Success,
                        Summary = Localizer["SavedTo", location],
                    }
                );
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ShowFailure(exception);
        }
    }

    private async Task DeleteDocument(DocumentDto document)
    {
        var confirmed = await DialogService.Confirm(
            Localizer["DeleteDocumentConfirm", document.Title],
            Localizer["DeleteDocumentTitle"],
            new ConfirmOptions
            {
                OkButtonText = Localizer["ConfirmDelete"],
                CancelButtonText = Localizer["ConfirmCancel"],
            }
        );

        if (confirmed == true)
        {
            errorKey = null;
            deleteDocument.Execute(new DeleteDocumentRequest { DocumentId = document.Id, Version = document.Version });
        }
    }

    private async Task DeleteArchive(ArchiveDto archive)
    {
        var confirmed = await DialogService.Confirm(
            Localizer["DeleteArchiveConfirm", archive.Name],
            Localizer["DeleteArchiveTitle"],
            new ConfirmOptions
            {
                OkButtonText = Localizer["ConfirmDelete"],
                CancelButtonText = Localizer["ConfirmCancel"],
            }
        );

        if (confirmed == true)
        {
            errorKey = null;
            deleteArchive.Execute(new DeleteArchiveRequest { ArchiveId = archive.Id, Version = archive.Version });
        }
    }

    public void Dispose() => disposables.Dispose();
}
