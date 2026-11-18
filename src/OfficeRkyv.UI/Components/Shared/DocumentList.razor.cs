using Fetchling.Core;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.UI.Files;
using OfficeRkyv.UI.Shared;
using Radzen;

namespace OfficeRkyv.UI.Components.Shared;

public partial class DocumentList
{
    // The value the "File into..." menu uses for the inbox; archive entries carry their archive id.
    private const string InboxMenuValue = "inbox";

    private IMutation<FileDocumentRequest, DocumentDto> fileDocument = null!;
    private IMutation<UnfileDocumentRequest, DocumentDto> unfileDocument = null!;
    private IMutation<DeleteDocumentRequest, Empty> deleteDocument = null!;

    /// <summary>The documents to list, as the page's query has them.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<DocumentDto> Items { get; set; } = [];

    /// <summary>The user's archives, for the "File into..." menu.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ArchiveDto> Archives { get; set; } = [];

    /// <summary>The localized heading of the empty state.</summary>
    [Parameter, EditorRequired]
    public string EmptyTitle { get; set; } = "";

    /// <summary>The localized text of the empty state.</summary>
    [Parameter, EditorRequired]
    public string EmptyText { get; set; } = "";

    protected override void OnInitialized()
    {
        // The mutations of this list: they start idle and only report executions started from here.
        fileDocument = Track(Documents.CreateFileDocumentMutation(), Failures.Clear);
        unfileDocument = Track(Documents.CreateUnfileDocumentMutation(), Failures.Clear);
        deleteDocument = Track(Documents.CreateDeleteDocumentMutation(), Failures.Clear);
    }

    // The other archives, plus the inbox when the document is filed. Choosing one files or unfiles it.
    private void OpenFileMenu(MouseEventArgs args, DocumentDto document)
    {
        var items = Archives
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
        Failures.Clear();

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
        Failures.Clear();

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
            Failures.Show(exception);
        }
    }

    private async Task OpenRenameDocumentDialog(DocumentDto document) =>
        await DialogService.OpenAsync<DocumentDialog>(
            Localizer["RenameDocument"],
            new Dictionary<string, object?> { [nameof(DocumentDialog.Document)] = document },
            new DialogOptions { Width = "28rem" }
        );

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
            Failures.Clear();
            deleteDocument.Execute(new DeleteDocumentRequest { DocumentId = document.Id, Version = document.Version });
        }
    }
}
