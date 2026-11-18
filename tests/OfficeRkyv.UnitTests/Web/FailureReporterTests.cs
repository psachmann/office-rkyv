using Grpc.Core;
using OfficeRkyv.UI.Auth;
using OfficeRkyv.UI.Components.Shared;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UnitTests.Web;

public sealed class FailureReporterTests
{
    private static readonly string[] ListKinds =
    [
        QueryKinds.Archives,
        QueryKinds.Inbox,
        QueryKinds.ArchiveDocuments,
        QueryKinds.Search,
    ];

    private readonly RecordingSignInService signIn = new();
    private readonly List<string[]> invalidations = [];

    private FailureReporter CreateReporter() => new(signIn, kinds => invalidations.Add(kinds));

    [Test]
    public async Task ARefusal_SetsTheKeyOfItsMessage()
    {
        var reporter = CreateReporter();

        reporter.Show(new RpcException(new Status(StatusCode.Aborted, "changed")));

        await Assert.That(reporter.Key).IsEqualTo("ChangedElsewhere");
    }

    [Test]
    public async Task AnUnknownFailure_ShowsTheGeneralMessage()
    {
        var reporter = CreateReporter();

        reporter.Show(new InvalidOperationException("something else"));

        await Assert.That(reporter.Key).IsEqualTo("ActionFailed");
    }

    [Test]
    public async Task ARefusalThatMeansOutOfDate_ReloadsTheListsItMayHaveMadeStale()
    {
        var reporter = CreateReporter();

        reporter.Show(new RpcException(new Status(StatusCode.Aborted, "changed")));

        await Assert.That(invalidations.Count).IsEqualTo(1);
        await Assert.That(invalidations[0]).IsEquivalentTo(ListKinds);
    }

    [Test]
    public async Task ARefusalThatIsNotOutOfDate_ReloadsNothing()
    {
        var reporter = CreateReporter();

        reporter.Show(new RpcException(new Status(StatusCode.Unavailable, "down")));

        await Assert.That(invalidations.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AnUnauthenticatedRefusal_EndsTheSignIn_AndShowsTheSignedOutMessage()
    {
        var reporter = CreateReporter();

        reporter.Show(new RpcException(new Status(StatusCode.Unauthenticated, "no")));

        await Assert.That(signIn.SignOuts).IsEqualTo(1);
        await Assert.That(reporter.Key).IsEqualTo("SignedOut");
    }

    [Test]
    public async Task AnOtherRefusal_DoesNotEndTheSignIn()
    {
        var reporter = CreateReporter();

        reporter.Show(new RpcException(new Status(StatusCode.Aborted, "changed")));

        await Assert.That(signIn.SignOuts).IsEqualTo(0);
    }

    [Test]
    public async Task Clear_HidesTheMessage_AndRaisesChanged()
    {
        var reporter = CreateReporter();
        var changes = 0;
        reporter.Changed += () => changes++;
        reporter.Show(new InvalidOperationException());

        reporter.Clear();

        await Assert.That(reporter.Key).IsNull();
        await Assert.That(changes).IsEqualTo(2);
    }

    [Test]
    public async Task Clear_WithNothingShown_RaisesNothing()
    {
        var reporter = CreateReporter();
        var changes = 0;
        reporter.Changed += () => changes++;

        reporter.Clear();

        await Assert.That(changes).IsEqualTo(0);
    }

    private sealed class RecordingSignInService : ISignInService
    {
        public int SignOuts { get; private set; }

        public Task SignInAsync(Guid userId) => Task.CompletedTask;

        public Task SignOutAsync()
        {
            SignOuts++;

            return Task.CompletedTask;
        }
    }
}
