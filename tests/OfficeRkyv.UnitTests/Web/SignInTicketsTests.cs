extern alias web;

using Microsoft.Extensions.Caching.Memory;
using web::OfficeRkyv.Web.Auth;

namespace OfficeRkyv.UnitTests.Web;

public sealed class SignInTicketsTests
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions());
    private readonly MovableTime time = new();

    private SignInTickets Tickets => new(cache, time);

    [Test]
    public async Task ASignInTicket_SignsInTheUserItWasIssuedFor_Once()
    {
        var tickets = Tickets;
        var alice = Guid.NewGuid();
        var ticket = tickets.IssueSignIn(alice);

        var first = tickets.RedeemSignIn(ticket);
        var second = tickets.RedeemSignIn(ticket);

        await Assert.That(first).IsEqualTo(alice);
        await Assert.That(second).IsNull();
    }

    [Test]
    public async Task ATicket_IsSixtySecondsOfValidity_ThenNothing()
    {
        var tickets = Tickets;
        var late = tickets.IssueSignIn(Guid.NewGuid());
        var inTime = tickets.IssueSignIn(Guid.NewGuid());

        time.Advance(SignInTickets.Lifetime - TimeSpan.FromSeconds(1));
        var redeemedInTime = tickets.RedeemSignIn(inTime);
        time.Advance(TimeSpan.FromSeconds(2));
        var redeemedLate = tickets.RedeemSignIn(late);

        await Assert.That(SignInTickets.Lifetime).IsEqualTo(TimeSpan.FromSeconds(60));
        await Assert.That(redeemedInTime).IsNotNull();
        await Assert.That(redeemedLate).IsNull();
    }

    [Test]
    public async Task ASignOutTicket_SignsNobodyIn_AndIsLeftForSigningOut()
    {
        var tickets = Tickets;
        var ticket = tickets.IssueSignOut();

        var asSignIn = tickets.RedeemSignIn(ticket);
        var asSignOut = tickets.RedeemSignOut(ticket);
        var again = tickets.RedeemSignOut(ticket);

        await Assert.That(asSignIn).IsNull();
        await Assert.That(asSignOut).IsTrue();
        await Assert.That(again).IsFalse();
    }

    [Test]
    public async Task ASignInTicket_CannotSignOut_AndIsLeftForSigningIn()
    {
        var tickets = Tickets;
        var alice = Guid.NewGuid();
        var ticket = tickets.IssueSignIn(alice);

        var asSignOut = tickets.RedeemSignOut(ticket);

        await Assert.That(asSignOut).IsFalse();
        await Assert.That(tickets.RedeemSignIn(ticket)).IsEqualTo(alice);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("made-up")]
    public async Task ATicketNobodyIssued_ChangesNothing(string? ticket)
    {
        var tickets = Tickets;

        await Assert.That(tickets.RedeemSignIn(ticket)).IsNull();
        await Assert.That(tickets.RedeemSignOut(ticket)).IsFalse();
    }

    [Test]
    public async Task Tickets_AreRandom_UrlSafe_AndTheLengthOfThirtyTwoBytes()
    {
        var tickets = Tickets;
        var issued = Enumerable.Range(0, 50).Select(_ => tickets.IssueSignIn(Guid.NewGuid())).ToList();

        await Assert.That(issued.Distinct()).Count().IsEqualTo(50);
        await Assert.That(issued.All(ticket => ticket.Length == 43)).IsTrue();
        await Assert
            .That(issued.All(ticket => ticket.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            .IsTrue();
    }

    [Test]
    public async Task TwoRequestsWithTheSameTicket_SignInOnlyOnce()
    {
        var tickets = Tickets;
        var ticket = tickets.IssueSignIn(Guid.NewGuid());

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => Task.Run(() => tickets.RedeemSignIn(ticket)))
        );

        await Assert.That(results.Count(result => result is not null)).IsEqualTo(1);
    }

    private sealed class MovableTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => now += by;

        public override DateTimeOffset GetUtcNow() => now;
    }
}
