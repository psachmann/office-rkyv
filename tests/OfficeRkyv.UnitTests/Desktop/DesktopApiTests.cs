using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Grpc.Core;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Users;
using OfficeRkyv.Desktop.Auth;
using OfficeRkyv.Desktop.Hosting;

namespace OfficeRkyv.UnitTests.Desktop;

/// <summary>The desktop app's API in its own process shape: a real API over a real unix socket, no window.</summary>
[SupportedOSPlatform("linux")]
public sealed class DesktopApiTests : IAsyncDisposable
{
    // Short on purpose: a unix socket path is limited to about 108 bytes.
    private readonly string root = Path.Combine(Path.GetTempPath(), $"ork-{Guid.NewGuid():n}"[..12]);
    private readonly List<DesktopApi> started = [];
    private readonly List<ServiceProvider> providers = [];

    private DesktopPaths Paths =>
        new(Path.Combine(root, "data"), Path.Combine(root, "config"), Path.Combine(root, "run"));

    // What the host makes at every start: the key its API validates caller tokens with and its clients sign with.
    private readonly byte[] key = RandomNumberGenerator.GetBytes(CallerTokens.MinimumKeyLength);

    private async Task<DesktopApi> Start()
    {
        var api = await DesktopApi.StartAsync(Paths, key);
        started.Add(api);

        return api;
    }

    /// <summary>The clients of one window, with whoever is signed in on <paramref name="signIn"/> (nobody by default).</summary>
    private ServiceProvider ClientsFor(DesktopApi api, DesktopSignIn? signIn = null, byte[]? signingKey = null)
    {
        var provider = DesktopApi
            .AddSocketClients(
                new ServiceCollection()
                    .AddLogging()
                    .AddSingleton<AuthenticationStateProvider>(signIn ?? new DesktopSignIn()),
                api.SocketPath,
                signingKey ?? key
            )
            .BuildServiceProvider();
        providers.Add(provider);

        return provider;
    }

    private UsersService.UsersServiceClient ClientFor(DesktopApi api) =>
        ClientsFor(api).GetRequiredService<UsersService.UsersServiceClient>();

    [Test]
    public async Task ARegisteredClient_ReachesTheApiOverTheSocket()
    {
        var api = await Start();
        var users = ClientFor(api);

        await users.CreateUserAsync(new CreateUserRequest { Email = "ada@example.com", DisplayName = "Ada" });
        var listed = await users.ListUsersAsync(new ListUsersRequest());

        await Assert.That(listed.Users.Select(user => user.Email)).IsEquivalentTo(["ada@example.com"]);
    }

    [Test]
    public async Task ACallThatNeedsACaller_WithoutAToken_IsUnauthenticated()
    {
        var api = await Start();
        var archives = ClientsFor(api).GetRequiredService<ArchivesService.ArchivesServiceClient>();

        var failure = await RefusalOf(async () => await archives.ListArchivesAsync(new ListArchivesRequest()));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.Unauthenticated);
    }

    // The registered clients are Wolverine's typed ones, which rethrow a failed call wrapped; the status is inside.
    private static async Task<RpcException> RefusalOf(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (Exception exception)
        {
            return exception as RpcException ?? (RpcException)exception.InnerException!;
        }

        throw new InvalidOperationException("Expected the call to be refused.");
    }

    [Test]
    public async Task ACallWithATokenSignedByTheHostsKey_IsAccepted()
    {
        var api = await Start();
        var signIn = new DesktopSignIn();
        var clients = ClientsFor(api, signIn);
        var ada = (
            await clients
                .GetRequiredService<UsersService.UsersServiceClient>()
                .CreateUserAsync(new CreateUserRequest { Email = "ada@example.com", DisplayName = "Ada" })
        ).User;
        signIn.SignIn(Guid.Parse(ada.Id));

        var archives = await clients
            .GetRequiredService<ArchivesService.ArchivesServiceClient>()
            .ListArchivesAsync(new ListArchivesRequest());

        await Assert.That(archives.Archives).IsEmpty();
    }

    [Test]
    public async Task ACallWithATokenSignedByAnotherKey_IsUnauthenticated()
    {
        var api = await Start();
        var signIn = new DesktopSignIn();
        var ada = (
            await ClientFor(api)
                .CreateUserAsync(new CreateUserRequest { Email = "ada@example.com", DisplayName = "Ada" })
        ).User;
        signIn.SignIn(Guid.Parse(ada.Id));
        var archives = ClientsFor(api, signIn, RandomNumberGenerator.GetBytes(CallerTokens.MinimumKeyLength))
            .GetRequiredService<ArchivesService.ArchivesServiceClient>();

        var failure = await RefusalOf(async () => await archives.ListArchivesAsync(new ListArchivesRequest()));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.Unauthenticated);
    }

    [Test]
    public async Task TheApiListensOnTheSocketOnly_NotOnANetworkPort()
    {
        var api = await Start();

        await Assert.That(api.Addresses).Count().IsEqualTo(1);
        await Assert.That(api.Addresses.Single()).StartsWith("http://unix:");
    }

    [Test]
    public async Task TheRuntimeDirectoryIsReadableByTheOwnerOnly()
    {
        await Start();

        await Assert
            .That(File.GetUnixFileMode(Paths.RuntimeDirectory))
            .IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Test]
    public async Task TheDataSurvivesARestart()
    {
        var first = await Start();
        await ClientFor(first)
            .CreateUserAsync(new CreateUserRequest { Email = "grace@example.com", DisplayName = "Grace" });
        await first.DisposeAsync();

        var second = await Start();
        var listed = await ClientFor(second).ListUsersAsync(new ListUsersRequest());

        await Assert.That(listed.Users.Select(user => user.Email)).IsEquivalentTo(["grace@example.com"]);
        await Assert.That(File.Exists(Paths.DatabasePath)).IsTrue();
    }

    [Test]
    public async Task TheSocketFileIsGoneAfterTheApiStops()
    {
        var api = await Start();
        await Assert.That(File.Exists(api.SocketPath)).IsTrue();

        await api.DisposeAsync();

        await Assert.That(File.Exists(Paths.SocketPath)).IsFalse();
    }

    [Test]
    public async Task ASocketLeftByACrash_DoesNotPreventTheNextStart()
    {
        // What a killed process leaves behind: a socket file that nothing listens on. A socket that is bound but
        // never listens does exactly that (connecting is refused). It stays open for the whole test, because
        // closing a bound socket makes .NET remove the file.
        Directory.CreateDirectory(Paths.RuntimeDirectory);
        using var dead = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        dead.Bind(new UnixDomainSocketEndPoint(Paths.SocketPath));

        await Assert.That(File.Exists(Paths.SocketPath)).IsTrue();

        var api = await Start();
        var listed = await ClientFor(api).ListUsersAsync(new ListUsersRequest());

        await Assert.That(listed.Users).IsEmpty();
    }

    [Test]
    public async Task AFileLeftAtTheSocketPath_DoesNotPreventTheNextStart()
    {
        Directory.CreateDirectory(Paths.RuntimeDirectory);
        await File.WriteAllTextAsync(Paths.SocketPath, "not a socket");

        var api = await Start();

        await Assert.That((await ClientFor(api).ListUsersAsync(new ListUsersRequest())).Users).IsEmpty();
    }

    [Test]
    public async Task AStartWhileAnotherInstanceRuns_IsRefused_AndTheFirstKeepsWorking()
    {
        var first = await Start();

        await Assert.That(async () => await DesktopApi.StartAsync(Paths, key)).Throws<AlreadyRunningException>();

        var listed = await ClientFor(first).ListUsersAsync(new ListUsersRequest());
        await Assert.That(listed.Users).IsEmpty();
        await Assert.That(File.Exists(first.SocketPath)).IsTrue();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var provider in providers)
        {
            await provider.DisposeAsync();
        }

        foreach (var api in started)
        {
            await api.DisposeAsync();
        }

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
