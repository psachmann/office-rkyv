using System.Reactive.Disposables;
using Fetchling.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using OfficeRkyv.UI.Auth;
using OfficeRkyv.UI.Components.Shared;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UI.Components.Layout;

public partial class DocumentsLayout : IDisposable
{
    private readonly CompositeDisposable disposables = [];
    private FailureReporter failures = null!;

    [CascadingParameter]
    private Task<AuthenticationState> AuthenticationState { get; set; } = null!;

    /// <summary>The page's reporter, cascaded to the page and everything in it.</summary>
    private FailureReporter Failures => failures;

    protected override void OnInitialized()
    {
        failures = new FailureReporter(SignIn, kinds => QueryClient.Invalidate(kinds));
        failures.Changed += OnFailuresChanged;
        disposables.Add(Disposable.Create(() => failures.Changed -= OnFailuresChanged));

        // A user the API does not know, or does not accept, is no longer signed in. A refused query ends the
        // sign-in as a refused mutation does.
        disposables.Add(
            Users.FindUserById.Success.Subscribe(found =>
            {
                if (found.User is null)
                {
                    _ = InvokeAsync(SignIn.SignOutAsync);
                }
            })
        );
        WatchForSignOut(Users.FindUserById);
        WatchForSignOut(Archives.FindArchives);
        WatchForSignOut(Documents.FindInbox);
        WatchForSignOut(Documents.FindArchiveDocuments);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (SignedInUser.IdOf((await AuthenticationState).User) is not { } userId)
        {
            return;
        }

        Users.FindUserById.SetArgs(userId);
        Archives.FindArchives.SetArgs(userId);
        Documents.FindInbox.SetArgs(userId);
    }

    private void OnFailuresChanged() => _ = InvokeAsync(StateHasChanged);

    private void WatchForSignOut<TArgs, TData>(IQuery<TArgs, TData> query)
        where TData : class =>
        disposables.Add(
            query.Failure.Subscribe(error =>
                _ = InvokeAsync(() => DocumentErrors.EndSignInIfRefusedAsync(error, SignIn))
            )
        );

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposables.Dispose();
        }
    }
}
