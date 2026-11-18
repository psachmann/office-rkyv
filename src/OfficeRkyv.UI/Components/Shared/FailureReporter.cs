using OfficeRkyv.UI.Auth;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UI.Components.Shared;

/// <summary>
/// Where a page's refusals are shown: one reporter per page, cascaded from its layout, so every refusal of the
/// page reaches the same alert. A refusal sets the key of its message (a <c>DocumentErrors</c> resource key), ends
/// the sign-in when the API no longer accepts the user, and reloads the lists it made out of date.
/// </summary>
/// <remarks>
/// The invalidation is a function and not the query client, so the rules are testable without a client. The layout
/// passes <c>QueryClient.Invalidate</c>.
/// </remarks>
public sealed class FailureReporter(ISignInService signIn, Action<string[]> invalidate)
{
    /// <summary>The kinds of list a refusal that means "out of date" makes stale.</summary>
    private static readonly string[] ListKinds =
    [
        QueryKinds.Archives,
        QueryKinds.Inbox,
        QueryKinds.ArchiveDocuments,
        QueryKinds.Search,
    ];

    /// <summary>The resource key of the message to show, or null when nothing is to be shown.</summary>
    public string? Key { get; private set; }

    /// <summary>Raised when <see cref="Key"/> changes, so the layout can show or hide its alert.</summary>
    public event Action? Changed;

    /// <summary>Shows why an action was refused, and makes the rest of the page consistent with that.</summary>
    public void Show(Exception exception)
    {
        var errors = DocumentErrors.From(exception);
        Key = errors.General ?? "ActionFailed";

        // The item changed or is gone: what is shown is out of date, so load it again.
        if (errors.ReloadNeeded)
        {
            invalidate(ListKinds);
        }

        _ = DocumentErrors.EndSignInIfRefusedAsync(exception, signIn);
        Changed?.Invoke();
    }

    /// <summary>Hides the message, after a successful action or when the page moves to another item.</summary>
    public void Clear()
    {
        if (Key is null)
        {
            return;
        }

        Key = null;
        Changed?.Invoke();
    }
}
