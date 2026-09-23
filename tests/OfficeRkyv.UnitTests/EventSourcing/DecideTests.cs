using OfficeRkyv.Api.Shared;
using OfficeRkyv.UnitTests.EventSourcing.Fixtures;

namespace OfficeRkyv.UnitTests.EventSourcing;

public class DecideTests
{
    [Test]
    public async Task Decide_WithValidCommand_ProducesExpectedEvents()
    {
        var state = new TestEntity { Id = Guid.NewGuid() };
        var decider = new RenameDecider();

        var payloads = decider.Decide(state, new RenameCommand("new name"));

        await Assert.That(payloads.Count).IsEqualTo(1);
        await Assert.That(payloads[0]).IsTypeOf<TestRenamed>();
    }

    [Test]
    public async Task Decide_WithInvalidCommand_ThrowsAndLeavesStateUntouched()
    {
        var state = new TestEntity { Id = Guid.NewGuid(), Name = "original" };
        var decider = new RenameDecider();

        await Assert.ThrowsAsync<InvariantViolationException>(() =>
        {
            decider.Decide(state, new RenameCommand(""));
            return Task.CompletedTask;
        });

        await Assert.That(state.Name).IsEqualTo("original");
    }
}
