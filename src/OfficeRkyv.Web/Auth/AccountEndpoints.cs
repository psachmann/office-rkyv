using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OfficeRkyv.UI.Auth;

namespace OfficeRkyv.Web.Auth;

/// <summary>
/// The two requests that change the sign-in cookie, each opened by <see cref="WebSignInService"/> with a ticket
/// (see <see cref="SignInTickets"/>). A ticket that is missing, used, expired or for the other action changes
/// nothing and sends the visitor to the landing page.
/// </summary>
public static class AccountEndpoints
{
    private const string SignInRoute = "/account/sign-in";
    private const string SignOutRoute = "/account/sign-out";

    public static string SignInPath(string ticket) => $"{SignInRoute}?ticket={Uri.EscapeDataString(ticket)}";

    public static string SignOutPath(string ticket) => $"{SignOutRoute}?ticket={Uri.EscapeDataString(ticket)}";

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            SignInRoute,
            async (string? ticket, SignInTickets tickets, HttpContext context) =>
            {
                if (tickets.RedeemSignIn(ticket) is not { } userId)
                {
                    return Results.LocalRedirect("/");
                }

                // Persistent: the cookie outlives the browser session, up to the scheme's lifetime.
                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    SignedInUser.PrincipalFor(userId),
                    new AuthenticationProperties { IsPersistent = true }
                );

                return Results.LocalRedirect("/workspace");
            }
        );

        endpoints.MapGet(
            SignOutRoute,
            async (string? ticket, SignInTickets tickets, HttpContext context) =>
            {
                if (tickets.RedeemSignOut(ticket))
                {
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }

                return Results.LocalRedirect("/");
            }
        );

        return endpoints;
    }
}
