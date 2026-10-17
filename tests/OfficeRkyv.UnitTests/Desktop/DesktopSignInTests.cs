using System.Runtime.Versioning;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using OfficeRkyv.Desktop.Auth;
using OfficeRkyv.UI.Auth;

namespace OfficeRkyv.UnitTests.Desktop;

[SupportedOSPlatform("linux")]
public sealed class DesktopSignInTests
{
    private static async Task<Guid?> SignedInOf(DesktopSignIn signIn) =>
        SignedInUser.IdOf((await signIn.GetAuthenticationStateAsync()).User);

    [Test]
    public async Task TheAppStartsSignedOut()
    {
        var state = await new DesktopSignIn().GetAuthenticationStateAsync();

        await Assert.That(state.User.Identity!.IsAuthenticated).IsFalse();
        await Assert.That(SignedInUser.IdOf(state.User)).IsNull();
    }

    [Test]
    public async Task SigningIn_MakesTheUserTheSignedInUser_AndRaisesTheChange()
    {
        var signIn = new DesktopSignIn();
        var alice = Guid.NewGuid();
        var raised = new List<Task<AuthenticationState>>();
        signIn.AuthenticationStateChanged += raised.Add;

        signIn.SignIn(alice);

        await Assert.That(await SignedInOf(signIn)).IsEqualTo(alice);
        await Assert.That(raised).Count().IsEqualTo(1);
        await Assert.That(SignedInUser.IdOf((await raised[0]).User)).IsEqualTo(alice);
    }

    [Test]
    public async Task SigningOut_ClearsTheUser_AndRaisesTheChange()
    {
        var signIn = new DesktopSignIn();
        signIn.SignIn(Guid.NewGuid());
        var raised = new List<Task<AuthenticationState>>();
        signIn.AuthenticationStateChanged += raised.Add;

        signIn.SignOut();

        await Assert.That(await SignedInOf(signIn)).IsNull();
        await Assert.That(raised).Count().IsEqualTo(1);
        await Assert.That(SignedInUser.IdOf((await raised[0]).User)).IsNull();
    }

    [Test]
    public async Task SigningInAsAnotherUser_ReplacesTheFirst()
    {
        var signIn = new DesktopSignIn();
        var bob = Guid.NewGuid();
        signIn.SignIn(Guid.NewGuid());

        signIn.SignIn(bob);

        await Assert.That(await SignedInOf(signIn)).IsEqualTo(bob);
    }

    [Test]
    public async Task EachChange_IsLogged_WithTheUserIdOnly()
    {
        var logger = new CapturingLogger();
        var signIn = new DesktopSignIn(logger);
        var alice = Guid.NewGuid();

        signIn.SignIn(alice);
        signIn.SignOut();

        await Assert
            .That(logger.Entries)
            .IsEquivalentTo([
                (LogLevel.Information, $"Signed in as user {alice}"),
                (LogLevel.Information, "Signed out"),
            ]);
    }

    [Test]
    public async Task TheWindowsSignInService_ChangesTheSharedState_AndSignsOutToTheLandingPage()
    {
        var signIn = new DesktopSignIn();
        var navigation = new RecordingNavigation();
        var service = new DesktopSignInService(signIn, navigation);
        var alice = Guid.NewGuid();

        await service.SignInAsync(alice);
        await Assert.That(await SignedInOf(signIn)).IsEqualTo(alice);

        await service.SignOutAsync();

        await Assert.That(await SignedInOf(signIn)).IsNull();
        await Assert.That(navigation.Visited).IsEquivalentTo(["http://localhost/"]);
    }

    private sealed class RecordingNavigation : NavigationManager
    {
        public RecordingNavigation() => Initialize("http://localhost/", "http://localhost/workspace");

        public List<string> Visited { get; } = [];

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            Visited.Add(ToAbsoluteUri(uri).ToString());
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
