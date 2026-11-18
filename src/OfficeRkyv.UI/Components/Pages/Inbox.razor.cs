using Microsoft.AspNetCore.Components;
using OfficeRkyv.UI.Components.Shared;
using Radzen;

namespace OfficeRkyv.UI.Components.Pages;

public partial class Inbox
{
    protected override void OnInitialized()
    {
        // A refusal from another page does not stay under this one.
        Failures.Clear();
    }

    // A new document always goes to the inbox, whichever page it is created from.
    private Task OpenCreateDocumentDialog() =>
        DialogService.OpenAsync<UploadDocumentsDialog>(
            Localizer["NewDocument"],
            options: new DialogOptions { Width = "28rem" }
        );
}
