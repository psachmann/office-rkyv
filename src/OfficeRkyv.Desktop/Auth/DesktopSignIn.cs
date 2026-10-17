using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeRkyv.UI.Auth;

namespace OfficeRkyv.Desktop.Auth;

/// <summary>
/// Who is signed in on the desktop: one user for the running app, kept in memory. Nothing is written to disk, so
/// every start is signed out. It is shared by every window, because a second launch opens a second window on the
/// running instance and one person runs the app.
/// </summary>
/// <remarks>
/// Both the UI's pages and the gRPC clients read it as the <see cref="AuthenticationStateProvider"/>: the
/// clients' call credentials issue the caller token for this user. Signing in and out, and showing the page
/// that follows, is <see cref="DesktopSignInService"/>.
/// </remarks>
internal sealed partial class DesktopSignIn(ILogger? logger = null) : AuthenticationStateProvider
{
    private readonly ILogger logger = logger ?? NullLogger.Instance;
    private volatile ClaimsPrincipal current = SignedInUser.Anonymous;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(current));

    public void SignIn(Guid userId)
    {
        Change(SignedInUser.PrincipalFor(userId));
        LogSignedIn(logger, userId);
    }

    public void SignOut()
    {
        Change(SignedInUser.Anonymous);
        LogSignedOut(logger);
    }

    private void Change(ClaimsPrincipal principal)
    {
        current = principal;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Signed in as user {UserId}")]
    private static partial void LogSignedIn(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Signed out")]
    private static partial void LogSignedOut(ILogger logger);
}
