using System.Diagnostics;
using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.UnitTests.Extraction;

public sealed class ProgramRunnerTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(30);

    private readonly IProgramRunner runner = new ProgramRunner();

    [Test]
    public async Task WhatTheProgramWrites_IsCaptured_OnBothStreams()
    {
        var result = await runner.RunAsync("sh", ["-c", "echo out; echo err >&2"], Generous);

        await Assert.That(result.Outcome).IsEqualTo(ProgramOutcome.Exited);
        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.StandardOutput).IsEqualTo("out\n");
        await Assert.That(result.StandardError).IsEqualTo("err\n");
    }

    [Test]
    public async Task Output_IsReadAsUtf8()
    {
        var result = await runner.RunAsync("sh", ["-c", "printf 'Größe 5 €'"], Generous);

        await Assert.That(result.StandardOutput).IsEqualTo("Größe 5 €");
    }

    [Test]
    public async Task ANonZeroExitCode_IsReported_NotThrown()
    {
        var result = await runner.RunAsync("sh", ["-c", "exit 3"], Generous);

        await Assert.That(result.Outcome).IsEqualTo(ProgramOutcome.Exited);
        await Assert.That(result.ExitCode).IsEqualTo(3);
        await Assert.That(result.Succeeded).IsFalse();
    }

    [Test]
    public async Task AProgramThatDoesNotExist_IsReportedAsNotStarted()
    {
        var result = await runner.RunAsync("no-such-program-office-rkyv", [], Generous);

        await Assert.That(result.Outcome).IsEqualTo(ProgramOutcome.NotStarted);
        await Assert.That(result.Succeeded).IsFalse();
    }

    [Test]
    public async Task ArgumentsAreNotReadByAShell()
    {
        // Passed through as one argument each: no expansion, no second command.
        var result = await runner.RunAsync("printf", ["%s|", "$HOME", "a; echo injected", "*"], Generous);

        await Assert.That(result.StandardOutput).IsEqualTo("$HOME|a; echo injected|*|");
    }

    [Test]
    public async Task AProgramOverTheLimit_IsKilled_AndReportedAsTimedOut()
    {
        var stopwatch = Stopwatch.StartNew();

        // `sh` stays the parent of `sleep`, so this also shows that the whole tree is killed: the pipes would
        // otherwise stay open for 30 seconds.
        var result = await runner.RunAsync("sh", ["-c", "sleep 30; echo done"], TimeSpan.FromMilliseconds(300));

        await Assert.That(result.Outcome).IsEqualTo(ProgramOutcome.TimedOut);
        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task Cancelling_KillsTheProgram_AndThrows()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var stopwatch = Stopwatch.StartNew();

        await Assert
            .That(async () => await runner.RunAsync("sh", ["-c", "sleep 30; echo done"], Generous, cancellation.Token))
            .Throws<OperationCanceledException>();

        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task AProgramThatReadsItsInput_DoesNotWaitForIt()
    {
        var result = await runner.RunAsync("cat", [], Generous);

        await Assert.That(result.Outcome).IsEqualTo(ProgramOutcome.Exited);
        await Assert.That(result.StandardOutput).IsEmpty();
    }
}
