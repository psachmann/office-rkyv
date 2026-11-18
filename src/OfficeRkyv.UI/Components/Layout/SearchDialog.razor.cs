using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.UI.Auth;
using OfficeRkyv.UI.Components.Shared;
using OfficeRkyv.UI.Shared;
using Radzen;
using Radzen.Blazor;

namespace OfficeRkyv.UI.Components.Layout;

public partial class SearchDialog
{
    // How long typing has to rest before the search is run: one search per pause, not one per key.
    private static readonly TimeSpan SearchPause = TimeSpan.FromMilliseconds(300);

    private readonly CompositeDisposable disposables = [];
    private readonly Subject<string> typed = new();
    private ElementReference container;
    private RadzenTextBox field = null!;
    private IJSObjectReference? module;
    private Guid userId;
    private string searchText = "";

    // What the results on show are for: the text once typing has rested. Only a query with a word the server
    // would match is sent; anything else shows the hint and sends nothing.
    private string appliedQuery = "";

    private string? downloadErrorKey;

    private bool HasSearchableWord => SearchTerms.From(appliedQuery).Count > 0;

    protected override async Task OnInitializedAsync()
    {
        // A user the API does not accept is no longer signed in, as on the document pages.
        WatchForSignOut(Documents.SearchDocuments);
        WatchForSignOut(Archives.FindArchives);

        disposables.Add(typed);
        disposables.Add(typed.Throttle(SearchPause).Subscribe(text => _ = InvokeAsync(() => ApplySearch(text))));

        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();

        if (SignedInUser.IdOf(state.User) is { } id)
        {
            userId = id;
            Archives.FindArchives.SetArgs(userId);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            await field.Element.FocusAsync();
            module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/OfficeRkyv.UI/js/search-dialog.js");
            await module.InvokeVoidAsync("listenForResultKeys", container);
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            // The dialog was closed before the page got to it (Escape right after opening): its elements are gone,
            // and an exception here would take the whole circuit down.
        }
    }

    private void WatchForSignOut<TArgs, TData>(IQuery<TArgs, TData> query)
        where TData : class =>
        disposables.Add(
            query.Failure.Subscribe(error =>
                _ = InvokeAsync(() => DocumentErrors.EndSignInIfRefusedAsync(error, SignIn))
            )
        );

    private void OnSearchInput(ChangeEventArgs args)
    {
        searchText = args.Value?.ToString() ?? "";
        typed.OnNext(searchText);
    }

    private void ApplySearch(string text)
    {
        appliedQuery = text.Trim();

        if (HasSearchableWord && userId != Guid.Empty)
        {
            Documents.SearchDocuments.SetArgs(new DocumentSearch(userId, appliedQuery));
        }

        StateHasChanged();
    }

    // Takes the user to where the document is. DialogService closes its dialogs when the address changes, but a
    // document in the entry that is already open changes nothing, so the dialog is closed here as well.
    private void Choose(DocumentDto document)
    {
        DialogService.Close();
        Navigation.NavigateTo(document.HasArchiveId ? Addresses.Archive(document.ArchiveId) : Addresses.Inbox);
    }

    // The refusal (unknown or deleted document) is thrown by opening the stream, so it is shown in the dialog and
    // nothing has been handed to the host yet. It is not a failed search, so it has its own place.
    private async Task Download(DocumentDto document)
    {
        downloadErrorKey = null;

        try
        {
            await using var content = await Documents.OpenDownloadAsync(document);
            var saved = await FileSaver.SaveAsync(document.FileName, document.ContentType, content);

            // The host says where the file went when it can: the desktop does, a browser has its own list.
            if (saved.Location is { } location)
            {
                NotificationService.Notify(
                    new NotificationMessage
                    {
                        Severity = NotificationSeverity.Success,
                        Summary = Localizer["SavedTo", location],
                    }
                );
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var errors = DocumentErrors.From(exception);
            downloadErrorKey = errors.General ?? "ActionFailed";
            _ = DocumentErrors.EndSignInIfRefusedAsync(exception, SignIn);

            // The item changed or is gone: what is shown is out of date, so load it again.
            if (errors.ReloadNeeded)
            {
                QueryClient.Invalidate(
                    QueryKinds.Archives,
                    QueryKinds.Inbox,
                    QueryKinds.ArchiveDocuments,
                    QueryKinds.Search
                );
            }
        }
    }

    private string LocationName(DocumentDto document, IReadOnlyList<ArchiveDto> archives) =>
        document.HasArchiveId
            ? archives.FirstOrDefault(archive => archive.Id == document.ArchiveId)?.Name ?? string.Empty
            : Localizer["InboxName"].Value;

    // What a refused search says: the text for its own refusal, and the general one otherwise.
    private string SearchFailureText(Exception error) =>
        DocumentErrors.From(error).Query is { } key ? ErrorLocalizer[key].Value : Localizer["SearchFailed"].Value;

    public async ValueTask DisposeAsync()
    {
        disposables.Dispose();

        if (module is not null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone, and the page with it.
            }
        }
    }
}
