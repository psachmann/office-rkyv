namespace OfficeRkyv.UI.Components.Layout;

public partial class MainLayout
{
    /// <summary>Where a page puts its own navigation entries in the sidebar, between the top menu and Settings.</summary>
    public static readonly object WorkspaceNavigationSection = new();

    public bool IsExpanded { get; set; } = true;
}
