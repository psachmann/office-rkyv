using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>A runner that starts nothing: it records what it was asked to run and answers from a function.</summary>
internal sealed class FakeProgramRunner(Func<string, IReadOnlyList<string>, ProgramResult> answer) : IProgramRunner
{
    public List<(string FileName, IReadOnlyList<string> Arguments, TimeSpan TimeLimit)> Calls { get; } = [];

    public Task<ProgramResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeLimit,
        CancellationToken cancellation = default
    )
    {
        Calls.Add((fileName, arguments, timeLimit));

        return Task.FromResult(answer(fileName, arguments));
    }
}
