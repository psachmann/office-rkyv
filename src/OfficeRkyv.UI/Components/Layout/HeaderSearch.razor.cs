using Microsoft.JSInterop;
using Radzen;

namespace OfficeRkyv.UI.Components.Layout;

public partial class HeaderSearch
{
    private IJSObjectReference? module;
    private DotNetObjectReference<HeaderSearch>? self;

    // The open dialog: the task ends when it closes. There is never more than one.
    private Task? dialog;

    private bool IsOpen => dialog is { IsCompleted: false };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        self = DotNetObjectReference.Create(this);
        module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/OfficeRkyv.UI/js/search-shortcut.js");
        await module.InvokeVoidAsync("register", self);
    }

    /// <summary>Called by the script when Ctrl+K or Cmd+K was pressed; <paramref name="aDialogIsOpen"/> is any dialog, ours too.</summary>
    [JSInvokable]
    public Task OnShortcut(bool aDialogIsOpen) =>
        InvokeAsync(async () =>
        {
            if (IsOpen)
            {
                await module!.InvokeVoidAsync("focusSearchField");
            }
            else if (!aDialogIsOpen)
            {
                await OpenAsync();
            }
        });

    private Task OpenAsync()
    {
        if (!IsOpen)
        {
            dialog = DialogService.OpenAsync<SearchDialog>(
                Localizer["SearchLabel"],
                options: new DialogOptions
                {
                    Width = "36rem",
                    Top = "10vh",
                    CssClass = "search-dialog",
                    CloseDialogOnEsc = true,
                    CloseDialogOnOverlayClick = true,
                }
            );
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        self?.Dispose();

        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("unregister");
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone, and the listener with the page.
            }
        }
    }
}
