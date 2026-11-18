using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace OfficeRkyv.Web.Components.Pages;

public partial class Error
{
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    private string? RequestId { get; set; }
    private bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    protected override void OnInitialized() => RequestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;

    private async Task CopyRequestIdAsync()
    {
        if (string.IsNullOrEmpty(RequestId))
        {
            return;
        }

        try
        {
            await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", RequestId);
            NotificationService.Notify(
                new NotificationMessage
                {
                    Severity = NotificationSeverity.Success,
                    Summary = Localizer["CopiedSummary"],
                    Detail = Localizer["CopiedDetail"],
                    Duration = 3000,
                }
            );
        }
        catch (JSException)
        {
            NotificationService.Notify(
                new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = Localizer["CopyFailedSummary"],
                    Detail = Localizer["CopyFailedDetail"],
                    Duration = 3000,
                }
            );
        }
    }
}
