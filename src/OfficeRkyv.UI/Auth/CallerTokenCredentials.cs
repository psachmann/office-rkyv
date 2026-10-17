using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OfficeRkyv.Contracts;

namespace OfficeRkyv.UI.Auth;

/// <summary>
/// Puts the caller token on every call a gRPC client makes, for the user signed in where the client was
/// resolved. The one registration both hosts use.
/// </summary>
public static class CallerTokenCredentials
{
    /// <summary>
    /// Adds a bearer token for the signed-in user to each call of the client, and nothing when nobody is signed
    /// in (the API refuses a call that needs a caller). The user is read from the
    /// <see cref="AuthenticationStateProvider"/> of the scope the client was resolved in: the circuit on the web,
    /// the window on the desktop.
    /// </summary>
    /// <param name="builder">The client's HTTP client builder.</param>
    /// <param name="key">The key the API validates with; at least <see cref="CallerTokens.MinimumKeyLength"/> bytes.</param>
    public static IHttpClientBuilder AddTo(IHttpClientBuilder builder, byte[] key) => AddTo(builder, key, time: null);

    internal static IHttpClientBuilder AddTo(IHttpClientBuilder builder, byte[] key, TimeProvider? time)
    {
        var issuer = new CallerTokenIssuer(key, time);

        return builder.AddCallCredentials(
            async (_, metadata, services) =>
            {
                var state = await services
                    .GetRequiredService<AuthenticationStateProvider>()
                    .GetAuthenticationStateAsync();

                if (SignedInUser.IdOf(state.User) is { } userId)
                {
                    metadata.Add("authorization", $"Bearer {issuer.Issue(userId)}");
                }
            }
        );
    }
}
