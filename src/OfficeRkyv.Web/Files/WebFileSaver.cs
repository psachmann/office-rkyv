using Microsoft.JSInterop;
using OfficeRkyv.UI.Files;

namespace OfficeRkyv.Web.Files;

/// <summary>
/// A download for the browser. The stream goes to the page over the circuit as a <see cref="DotNetStreamReference"/>
/// and a small script saves it, so the file arrives in the browser's own download list. The browser decides
/// where it goes, which is why there is no location to report.
/// </summary>
internal sealed class WebFileSaver(IJSRuntime javaScript) : IFileSaver, IAsyncDisposable
{
    private IJSObjectReference? module;

    public async Task<FileSaveResult> SaveAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellation = default
    )
    {
        module ??= await javaScript.InvokeAsync<IJSObjectReference>("import", cancellation, "./js/save-file.js");

        // The caller owns the stream and disposes it.
        using var reference = new DotNetStreamReference(content, leaveOpen: true);
        await module.InvokeVoidAsync("saveFile", cancellation, fileName, contentType, reference);

        return new FileSaveResult(true);
    }

    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is already gone, and so is the module.
            }
        }
    }
}
