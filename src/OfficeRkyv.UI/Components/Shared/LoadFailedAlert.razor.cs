using Microsoft.AspNetCore.Components;
using Radzen;

namespace OfficeRkyv.UI.Components.Shared;

public partial class LoadFailedAlert
{
    /// <summary>The localized text of the message.</summary>
    [Parameter, EditorRequired]
    public string Message { get; set; } = "";

    /// <summary>Attributes such as a test id, passed to the alert.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }
}
