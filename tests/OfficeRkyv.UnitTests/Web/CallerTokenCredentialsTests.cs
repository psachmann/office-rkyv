using System.Net;
using System.Net.Http.Headers;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Users;
using OfficeRkyv.UI.Auth;
using Wolverine.Grpc.Client;

namespace OfficeRkyv.UnitTests.Web;

public sealed class CallerTokenCredentialsTests
{
    /// <summary>
    /// Design D9 rests on this: the delegate of <c>AddCallCredentials</c> gets the scope the client was resolved
    /// in (the circuit on the web, the window on the desktop), not the root provider. If it did not, the signed-in
    /// user of one circuit could not be told apart from another's.
    /// </summary>
    [Test]
    public async Task CallCredentials_ResolveServices_FromTheScopeTheClientWasResolvedIn()
    {
        var seen = new List<ScopeMarker>();
        var services = new ServiceCollection();
        services.AddScoped<ScopeMarker>();

        var client = services.AddWolverineGrpcClient<UsersService.UsersServiceClient>(options =>
            options.Address = new Uri("http://localhost")
        );
        client
            .HttpClientBuilder!.ConfigurePrimaryHttpMessageHandler(() => new EmptyReplyHandler())
            .ConfigureChannel(options => options.UnsafeUseInsecureChannelCallCredentials = true)
            .AddCallCredentials(
                (_, metadata, serviceProvider) =>
                {
                    seen.Add(serviceProvider.GetRequiredService<ScopeMarker>());
                    metadata.Add("authorization", "Bearer test");

                    return Task.CompletedTask;
                }
            );

        // Scope validation makes resolving a scoped service from the root provider throw, so a delegate that was
        // handed the root fails this test instead of passing by luck.
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
        );

        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        await first.ServiceProvider.GetRequiredService<UsersService.UsersServiceClient>().ListUsersAsync(new());
        await second.ServiceProvider.GetRequiredService<UsersService.UsersServiceClient>().ListUsersAsync(new());

        await Assert.That(seen).Count().IsEqualTo(2);
        await Assert.That(seen[0]).IsSameReferenceAs(first.ServiceProvider.GetRequiredService<ScopeMarker>());
        await Assert.That(seen[1]).IsSameReferenceAs(second.ServiceProvider.GetRequiredService<ScopeMarker>());
        await Assert.That(seen[0]).IsNotSameReferenceAs(seen[1]);
    }

    [Test]
    public async Task ACallByASignedInUser_CarriesAValidBearerTokenForThatUser()
    {
        var handler = new EmptyReplyHandler();
        await using var provider = CreateProvider(handler);
        await using var scope = provider.CreateAsyncScope();
        var alice = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<SignedInState>().SignIn(alice);

        await scope.ServiceProvider.GetRequiredService<UsersService.UsersServiceClient>().ListUsersAsync(new());

        var authorization = handler.Authorizations.Single();
        await Assert.That(authorization!.Scheme).IsEqualTo("Bearer");
        var result = await ValidateAsync(authorization.Parameter!, PipelineHost.CallerKey);
        await Assert.That(result.IsValid).IsTrue();
        var token = (JsonWebToken)result.SecurityToken;
        await Assert.That(token.Subject).IsEqualTo(alice.ToString());
        await Assert.That(token.ValidTo - DateTime.UtcNow).IsLessThanOrEqualTo(CallerTokens.Lifetime);
        await Assert.That(token.ValidTo - DateTime.UtcNow).IsGreaterThan(TimeSpan.FromMinutes(4));
    }

    [Test]
    public async Task ACallByASignedOutVisitor_CarriesNoAuthorizationHeader()
    {
        var handler = new EmptyReplyHandler();
        await using var provider = CreateProvider(handler);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<UsersService.UsersServiceClient>().ListUsersAsync(new());

        await Assert.That(handler.Authorizations.Single()).IsNull();
    }

    [Test]
    public async Task ATokenIsIssuedForEachCall_ForTheUserSignedInAtThatMoment()
    {
        var handler = new EmptyReplyHandler();
        await using var provider = CreateProvider(handler);
        await using var scope = provider.CreateAsyncScope();
        var state = scope.ServiceProvider.GetRequiredService<SignedInState>();
        var client = scope.ServiceProvider.GetRequiredService<UsersService.UsersServiceClient>();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        state.SignIn(alice);
        await client.ListUsersAsync(new());
        state.SignIn(bob);
        await client.ListUsersAsync(new());
        state.SignOut();
        await client.ListUsersAsync(new());

        var subjects = new List<string?>();

        foreach (var authorization in handler.Authorizations)
        {
            subjects.Add(
                authorization is null
                    ? null
                    : (
                        (JsonWebToken)
                            (await ValidateAsync(authorization.Parameter!, PipelineHost.CallerKey)).SecurityToken
                    ).Subject
            );
        }

        await Assert.That(subjects).IsEquivalentTo([alice.ToString(), bob.ToString(), null]);
    }

    [Test]
    public async Task TheTokenIsRefused_WithAnotherKey()
    {
        var handler = new EmptyReplyHandler();
        await using var provider = CreateProvider(handler);
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SignedInState>().SignIn(Guid.NewGuid());
        await scope.ServiceProvider.GetRequiredService<UsersService.UsersServiceClient>().ListUsersAsync(new());

        var result = await ValidateAsync(handler.Authorizations.Single()!.Parameter!, PipelineHost.OtherKey);

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task AKeyShorterThanTheMinimum_IsRefused()
    {
        var services = new ServiceCollection();
        var builder = services
            .AddWolverineGrpcClient<UsersService.UsersServiceClient>(options =>
                options.Address = new Uri("http://localhost")
            )
            .HttpClientBuilder!;

        await Assert
            .That(() => CallerTokenCredentials.AddTo(builder, new byte[CallerTokens.MinimumKeyLength - 1]))
            .Throws<ArgumentException>();
    }

    private static ServiceProvider CreateProvider(EmptyReplyHandler handler)
    {
        var services = new ServiceCollection();
        services.AddScoped<SignedInState>();
        services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<SignedInState>());

        var http = services
            .AddWolverineGrpcClient<UsersService.UsersServiceClient>(options =>
                options.Address = new Uri("http://localhost")
            )
            .HttpClientBuilder!;
        http.ConfigurePrimaryHttpMessageHandler(() => handler)
            // The fake server is plain HTTP; the web host reaches the API over TLS, where this is not needed.
            .ConfigureChannel(options => options.UnsafeUseInsecureChannelCallCredentials = true);
        CallerTokenCredentials.AddTo(http, PipelineHost.CallerKey);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    // The API's own validation rules, applied to the token the client sent.
    private static Task<TokenValidationResult> ValidateAsync(string token, byte[] key) =>
        new JsonWebTokenHandler().ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidIssuer = CallerTokens.Issuer,
                ValidAudience = CallerTokens.Audience,
            }
        );

    private sealed class ScopeMarker;

    /// <summary>Who is signed in where a client was resolved: the circuit's or the window's authentication state.</summary>
    private sealed class SignedInState : AuthenticationStateProvider
    {
        private System.Security.Claims.ClaimsPrincipal user = SignedInUser.Anonymous;

        public void SignIn(Guid userId) => user = SignedInUser.PrincipalFor(userId);

        public void SignOut() => user = SignedInUser.Anonymous;

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }

    /// <summary>Answers every call with an empty message and status OK, as a server would, and notes who called.</summary>
    private sealed class EmptyReplyHandler : HttpMessageHandler
    {
        public List<AuthenticationHeaderValue?> Authorizations { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Authorizations.Add(request.Headers.Authorization);

            // One gRPC frame: not compressed, zero bytes of message.
            var content = new ByteArrayContent([0, 0, 0, 0, 0]);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Version = HttpVersion.Version20,
                Content = content,
            };
            response.TrailingHeaders.Add("grpc-status", "0");

            return Task.FromResult(response);
        }
    }
}
