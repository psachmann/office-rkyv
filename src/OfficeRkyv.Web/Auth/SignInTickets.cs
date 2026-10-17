using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace OfficeRkyv.Web.Auth;

/// <summary>
/// The one-time tickets that turn a click inside a circuit into a cookie. A circuit cannot set a cookie, so a
/// sign-in or sign-out is a real request to <c>/account/sign-in</c> or <c>/account/sign-out</c>; the ticket in
/// its address proves that request was started by this app's own page and not by a link or form elsewhere
/// (login CSRF): only a circuit can mint one, it works once, and it names what it is for.
/// </summary>
/// <remarks>
/// Held in memory by the one web process. A restart only loses tickets that are seconds old; the visitor clicks
/// again.
/// </remarks>
public sealed class SignInTickets(IMemoryCache cache, TimeProvider time)
{
    /// <summary>How long a ticket can be redeemed after it was issued.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private const int TicketBytes = 32;

    // Taking a ticket reads it and removes it; two requests with the same ticket must not both succeed.
    private readonly Lock gate = new();

    /// <summary>A ticket to sign in as the user.</summary>
    public string IssueSignIn(Guid userId) => Issue(new Entry(Purpose.SignIn, userId, time.GetUtcNow() + Lifetime));

    /// <summary>A ticket to sign out.</summary>
    public string IssueSignOut() => Issue(new Entry(Purpose.SignOut, Guid.Empty, time.GetUtcNow() + Lifetime));

    /// <summary>
    /// The user to sign in, once, for a sign-in ticket that is known, unused and unexpired. Null for anything
    /// else, including a sign-out ticket, which is left unused.
    /// </summary>
    public Guid? RedeemSignIn(string? ticket) => Redeem(ticket, Purpose.SignIn)?.UserId;

    /// <summary>Whether the sign-out ticket was known, unused and unexpired. It can be redeemed once.</summary>
    public bool RedeemSignOut(string? ticket) => Redeem(ticket, Purpose.SignOut) is not null;

    private string Issue(Entry entry)
    {
        var ticket = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TicketBytes));
        cache.Set(ticket, entry, Lifetime);

        return ticket;
    }

    private Entry? Redeem(string? ticket, Purpose purpose)
    {
        if (string.IsNullOrEmpty(ticket))
        {
            return null;
        }

        lock (gate)
        {
            if (!cache.TryGetValue(ticket, out Entry? entry) || entry is null || entry.Purpose != purpose)
            {
                return null;
            }

            cache.Remove(ticket);

            // The cache expires the entry too, but on its own clock; this one is the one that is tested.
            return entry.ExpiresAt > time.GetUtcNow() ? entry : null;
        }
    }

    private enum Purpose
    {
        SignIn,
        SignOut,
    }

    private sealed record Entry(Purpose Purpose, Guid UserId, DateTimeOffset ExpiresAt);
}
