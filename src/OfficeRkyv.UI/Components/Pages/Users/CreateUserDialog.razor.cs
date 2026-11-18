using System.Reactive.Disposables;
using Microsoft.AspNetCore.Components;
using OfficeRkyv.Contracts.Users;

namespace OfficeRkyv.UI.Components.Pages.Users;

public partial class CreateUserDialog
{
    private readonly CreateUserModel model = new();
    private IMutation<CreateUserRequest, UserDto> createUser = null!;
    private IDisposable? subscriptions;
    private CreateUserErrors errors = new();
    private bool isBusy;

    protected override void OnInitialized()
    {
        // This dialog's own mutation: it starts idle and only reports executions started from here.
        createUser = Users.CreateCreateUserMutation();
        subscriptions = new CompositeDisposable(
            // Closes with the user it created, so the landing page can sign in as them.
            createUser.Success.Subscribe(user => _ = InvokeAsync(() => DialogService.Close(user))),
            createUser.Failure.Subscribe(error => _ = InvokeAsync(() => ShowFailure(error)))
        );
    }

    // Only obvious mistakes are stopped here (Radzen's validators); whether the input is really valid,
    // and whether the email is taken, is the server's call and comes back through the mutation state.
    private void Submit()
    {
        errors = new();
        isBusy = true;
        createUser.Execute(new CreateUserRequest { Email = model.Email, DisplayName = model.DisplayName });
    }

    private void ShowFailure(Exception exception)
    {
        errors = CreateUserErrors.From(exception);
        isBusy = false;
        StateHasChanged();
    }

    public void Dispose()
    {
        subscriptions?.Dispose();
        createUser?.Dispose();
    }

    private sealed class CreateUserModel
    {
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }
}
