using Microsoft.AspNetCore.Components;

namespace OfficeRkyv.UI.Components.Layout;

public partial class SidebarUser
{
    /// <summary>The signed-in user.</summary>
    [Parameter, EditorRequired]
    public Guid UserId { get; set; }

    protected override void OnParametersSet() => Users.FindUserById.SetArgs(UserId);
}
