using Fetchling.Core;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Components;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.UI.Components.Shared;
using OfficeRkyv.UI.Shared;
using Radzen;

namespace OfficeRkyv.UI.Components.Pages;

public partial class Archive
{
    private IMutation<DeleteArchiveRequest, Empty> deleteArchive = null!;

    // The archive this page showed last: the same page instance is reused for another archive, so a change of the id
    // starts it afresh.
    private Guid? shownId;

    // Whether the archive has been seen on this page; an archive that vanishes after that is deleted elsewhere.
    private bool seenBefore;

    /// <summary>The archive to show, from the address.</summary>
    [Parameter]
    public Guid ArchiveId { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    protected override void OnInitialized()
    {
        // This page's own mutation: it starts idle and only reports executions started from here.
        deleteArchive = Track(Archives.CreateDeleteArchiveMutation(), OnArchiveDeleted);

        // The archive list decides whether the address names one of the user's archives. A list that is loaded
        // only later arrives through Success; one that was loaded already arrives through OnParametersSet.
        Own(Archives.FindArchives.Success.Subscribe(archives => _ = InvokeAsync(() => Resolve(archives))));
    }

    protected override void OnParametersSet()
    {
        if (shownId != ArchiveId)
        {
            shownId = ArchiveId;
            seenBefore = false;
            Failures.Clear();
        }

        Documents.FindArchiveDocuments.SetArgs(ArchiveId);

        if (Archives.FindArchives.CurrentState is { IsSuccess: true, CurrentData: { } archives })
        {
            Resolve(archives);
        }
    }

    private ArchiveDto? Find(IReadOnlyList<ArchiveDto> archives) =>
        archives.FirstOrDefault(archive => Guid.TryParse(archive.Id, out var id) && id == ArchiveId);

    // A list that is loading or has failed decides nothing: the page shows its own loading or failure state.
    private void Resolve(IReadOnlyList<ArchiveDto> archives)
    {
        switch (ArchiveResolution.Resolve(archives, ArchiveId, seenBefore))
        {
            case ArchiveLookup.Found:
                seenBefore = true;
                break;
            case ArchiveLookup.NotFound:
                Navigation.NavigateTo("/not-found", replace: true);
                break;
            case ArchiveLookup.Vanished:
                Navigation.NavigateTo(Addresses.Inbox, replace: true);
                break;
        }
    }

    private void OnArchiveDeleted()
    {
        // Only the shown archive can be deleted from this page, so the inbox is where its documents went.
        Failures.Clear();
        Navigation.NavigateTo(Addresses.Inbox, replace: true);
    }

    private Task OpenRenameArchiveDialog(ArchiveDto archive) =>
        DialogService.OpenAsync<ArchiveDialog>(
            Localizer["RenameArchive"],
            new Dictionary<string, object?> { [nameof(ArchiveDialog.Archive)] = archive },
            new DialogOptions { Width = "28rem" }
        );

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
            Failures.Clear();
            deleteArchive.Execute(new DeleteArchiveRequest { ArchiveId = archive.Id, Version = archive.Version });
        }
    }
}
