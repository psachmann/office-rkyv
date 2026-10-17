using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace OfficeRkyv.Api.Shared.Extraction;

public enum ProgramOutcome
{
    /// <summary>The program ran and ended by itself; see the exit code.</summary>
    Exited,

    /// <summary>The program could not be started: it is not installed, or not allowed to run.</summary>
    NotStarted,

    /// <summary>The program ran longer than it was allowed to, and was killed.</summary>
    TimedOut,
}

/// <param name="StandardOutput">What the program wrote to standard output; not to be logged.</param>
/// <param name="StandardError">What the program wrote to standard error; not to be logged.</param>
public sealed record ProgramResult(
    ProgramOutcome Outcome,
    int ExitCode = -1,
    string StandardOutput = "",
    string StandardError = ""
)
{
    public bool Succeeded => Outcome is ProgramOutcome.Exited && ExitCode == 0;
}

/// <summary>Runs an external program and reports how it went, without throwing for the program's own failures.</summary>
public interface IProgramRunner
{
    /// <summary>
    /// Runs the program to its end, or kills it after <paramref name="timeLimit"/>. Cancelling kills it and
    /// throws <see cref="OperationCanceledException"/>: that is the host stopping, not a failure of the program.
    /// </summary>
    Task<ProgramResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeLimit,
        CancellationToken cancellation = default
    );
}

/// <summary>
/// Starts the program as a process of its own, from a file name and an argument list and with no shell, so
/// nothing in an argument is ever read as shell syntax. A process that dies is an exit code here and not a crash
/// of the app, which matters on the desktop where the API shares its process with the window.
/// </summary>
internal sealed class ProgramRunner : IProgramRunner
{
    // How long to wait for the pipes to drain after the process is gone.
    private static readonly TimeSpan DrainLimit = TimeSpan.FromSeconds(5);

    public async Task<ProgramResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeLimit,
        CancellationToken cancellation = default
    )
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            return new ProgramResult(ProgramOutcome.NotStarted);
        }

        // Nothing is read from standard input; closing it keeps a program that waits for some from waiting.
        process.StandardInput.Close();

        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(timeLimit);

        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            await Drain(process, output, error);

            if (cancellation.IsCancellationRequested)
            {
                throw;
            }

            return new ProgramResult(ProgramOutcome.TimedOut);
        }

        await Drain(process, output, error);

        return new ProgramResult(ProgramOutcome.Exited, process.ExitCode, await output, await error);
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            // It ended between the check and the kill.
        }
    }

    private static async Task Drain(Process process, Task<string> output, Task<string> error)
    {
        using var drain = new CancellationTokenSource(DrainLimit);

        try
        {
            await process.WaitForExitAsync(drain.Token);
            await Task.WhenAll(output, error).WaitAsync(drain.Token);
        }
        catch (OperationCanceledException)
        {
            // A grandchild that kept a pipe open; the process itself is dealt with.
        }
    }
}
