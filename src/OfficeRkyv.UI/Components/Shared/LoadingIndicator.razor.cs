using Microsoft.AspNetCore.Components;
using Radzen;

namespace OfficeRkyv.UI.Components.Shared;

public partial class LoadingIndicator
{
    /// <summary>The size of the spinner; the sidebar's entries use the small one.</summary>
    [Parameter]
    public ProgressBarCircularSize Size { get; set; } = ProgressBarCircularSize.Medium;
}
