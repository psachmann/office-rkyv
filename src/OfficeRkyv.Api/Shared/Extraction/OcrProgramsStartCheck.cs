using Microsoft.Extensions.Options;

namespace OfficeRkyv.Api.Shared.Extraction;

/// <summary>
/// Runs each OCR program once when the host starts and logs what it found: one Information line when both are
/// installed and the configured languages are available, one Warning naming what is missing otherwise. Missing
/// programs are not an error (pages that need OCR are counted as unread), so this never stops the host.
/// </summary>
internal sealed partial class OcrProgramsStartCheck(
    IProgramRunner runner,
    IOptions<TextExtractionOptions> options,
    ILogger<OcrProgramsStartCheck> logger
) : IHostedService
{
    // A version query answers at once; a program that does not is not working.
    private static readonly TimeSpan QueryLimit = TimeSpan.FromSeconds(10);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CheckAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The check is a courtesy; whatever went wrong with it, the app starts.
            LogCheckFailed(logger, exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CheckAsync(CancellationToken cancellation)
    {
        var settings = options.Value;
        var problems = new List<string>();

        var tesseract = await VersionOfAsync(settings.TesseractPath, ["--version"], cancellation);
        var pdftoppm = await VersionOfAsync(settings.PdftoppmPath, ["-v"], cancellation);

        if (tesseract.Problem is not null)
        {
            problems.Add($"{settings.TesseractPath} {tesseract.Problem}");
        }

        if (pdftoppm.Problem is not null)
        {
            problems.Add($"{settings.PdftoppmPath} {pdftoppm.Problem}");
        }

        if (tesseract.Problem is null)
        {
            var installed = await LanguagesAsync(settings.TesseractPath, cancellation);
            var missing = settings.LanguageCodes.Where(code => !installed.Contains(code)).ToList();

            if (missing.Count > 0)
            {
                problems.Add($"language data is missing for {string.Join(", ", missing)}");
            }
        }

        if (problems.Count == 0)
        {
            LogAvailable(logger, tesseract.Version, pdftoppm.Version, settings.Languages);
        }
        else
        {
            LogUnavailable(logger, string.Join("; ", problems));
        }
    }

    private async Task<(string? Version, string? Problem)> VersionOfAsync(
        string program,
        string[] arguments,
        CancellationToken cancellation
    )
    {
        var result = await runner.RunAsync(program, arguments, QueryLimit, cancellation);

        return result.Outcome switch
        {
            ProgramOutcome.NotStarted => (null, "was not found"),
            ProgramOutcome.TimedOut => (null, "did not answer"),
            _ when result.ExitCode != 0 => (null, $"failed with exit code {result.ExitCode}"),
            // pdftoppm answers on standard error, tesseract on standard output.
            _ => (FirstLine(result.StandardOutput) ?? FirstLine(result.StandardError) ?? program, null),
        };
    }

    private async Task<HashSet<string>> LanguagesAsync(string tesseract, CancellationToken cancellation)
    {
        var result = await runner.RunAsync(tesseract, ["--list-langs"], QueryLimit, cancellation);

        // The first line is a header ("List of available languages in ..."); the rest is one code per line.
        return result.Succeeded
            ? result
                .StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Skip(1)
                .ToHashSet(StringComparer.Ordinal)
            : [];
    }

    private static string? FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Scanned pages will be read by OCR: {Tesseract}, {Pdftoppm}, languages {Languages}"
    )]
    private static partial void LogAvailable(ILogger logger, string? tesseract, string? pdftoppm, string languages);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Scanned pages will not be read, because OCR is not available: {Problems}"
    )]
    private static partial void LogUnavailable(ILogger logger, string problems);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not check whether OCR is available")]
    private static partial void LogCheckFailed(ILogger logger, Exception exception);
}
