using Radzen;

namespace OfficeRkyv.UI.Components.Shared;

public partial class DocumentNavigation
{
    private Task OpenCreateArchiveDialog() =>
        DialogService.OpenAsync<ArchiveDialog>(Localizer["NewArchive"], options: new DialogOptions { Width = "28rem" });
}
