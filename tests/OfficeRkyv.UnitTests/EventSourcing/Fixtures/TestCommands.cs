using OfficeRkyv.Api.Shared;

namespace OfficeRkyv.UnitTests.EventSourcing.Fixtures;

internal sealed record CreateCommand(string Name);

internal sealed class CreateDecider : IDecide<TestEntity, CreateCommand>
{
    public IReadOnlyList<object> Decide(TestEntity state, CreateCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new InvariantViolationException("Name must not be empty.");
        }

        return [new TestCreated(command.Name)];
    }
}

internal sealed record RenameCommand(string Name);

internal sealed class RenameDecider : IDecide<TestEntity, RenameCommand>
{
    public IReadOnlyList<object> Decide(TestEntity state, RenameCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new InvariantViolationException("Name must not be empty.");
        }

        return [new TestRenamed(command.Name)];
    }
}

internal sealed record DeleteCommand;

internal sealed class DeleteDecider : IDecide<TestEntity, DeleteCommand>
{
    public IReadOnlyList<object> Decide(TestEntity state, DeleteCommand command) => [new Deleted()];
}
