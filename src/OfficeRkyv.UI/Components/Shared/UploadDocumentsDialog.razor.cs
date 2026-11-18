using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using OfficeRkyv.Contracts;
using OfficeRkyv.UI.Components.Shared;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UI.Components.Shared;

public partial class UploadDocumentsDialog
{
    private readonly List<UploadItem> items = [];
    private readonly CancellationTokenSource cancellation = new();
    private bool isUploading;

    private async Task OnFilesChosen(InputFileChangeEventArgs args)
    {
        var batch = args.GetMultipleFiles(maximumFileCount: 1000).Select(file => new UploadItem(file)).ToList();
        items.AddRange(batch);
        isUploading = true;

        foreach (var item in batch)
        {
            await UploadAsync(item);
        }

        isUploading = false;
    }

    private async Task UploadAsync(UploadItem item)
    {
        // A quick answer for the obvious case; the server's limit is the authority.
        if (item.File.Size > FileLimits.DefaultMaxFileSizeBytes)
        {
            item.Refuse("ContentTooLarge");
            return;
        }

        item.State = UploadState.Uploading;
        StateHasChanged();

        try
        {
            await using var content = item.File.OpenReadStream(FileLimits.DefaultMaxFileSizeBytes, cancellation.Token);

            await Documents.UploadAsync(
                item.File.Name,
                item.File.ContentType,
                content,
                sent =>
                {
                    item.Sent = sent;
                    _ = InvokeAsync(StateHasChanged);
                },
                cancellation.Token
            );

            item.State = UploadState.Done;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var errors = DocumentErrors.From(exception);
            item.Refuse(errors.File ?? errors.General ?? "ActionFailed");
            _ = DocumentErrors.EndSignInIfRefusedAsync(exception, SignIn);
        }

        StateHasChanged();
    }

    private string StateText(UploadItem item) =>
        item.State switch
        {
            UploadState.Waiting => Localizer["Waiting"],
            UploadState.Uploading => Localizer["Uploading"],
            UploadState.Done => Localizer["Done"],
            _ => ErrorLocalizer[item.ErrorKey ?? "ActionFailed"],
        };

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private enum UploadState
    {
        Waiting,
        Uploading,
        Done,
        Refused,
    }

    private sealed class UploadItem(IBrowserFile file)
    {
        public IBrowserFile File { get; } = file;

        public string Name => File.Name;

        public UploadState State { get; set; } = UploadState.Waiting;

        public long Sent { get; set; }

        public string? ErrorKey { get; private set; }

        public double Percent => File.Size == 0 ? 0 : 100.0 * Sent / File.Size;

        public void Refuse(string errorKey)
        {
            State = UploadState.Refused;
            ErrorKey = errorKey;
        }
    }
}
