using Microsoft.Extensions.Options;

namespace OfficeRkyv.Api.Shared.Extraction;

/// <summary>Reads an image with <c>tesseract</c>, which writes the text to its standard output.</summary>
internal sealed partial class TesseractImageReader(
    IProgramRunner runner,
    IOptions<TextExtractionOptions> options,
    ILogger<TesseractImageReader> logger
) : IImageReader
{
    public async Task<string?> ReadAsync(
        string sha256,
        int pageNumber,
        string imagePath,
        CancellationToken cancellation = default
    )
    {
        var result = await runner.RunAsync(
            options.Value.TesseractPath,
            [imagePath, "stdout", "-l", options.Value.Languages],
            options.Value.TimeLimit,
            cancellation
        );

        switch (result.Outcome)
        {
            case ProgramOutcome.NotStarted:
                // Said once, when the host started (OcrProgramsStartCheck), and not again for every page.
                return null;
            case ProgramOutcome.TimedOut:
                LogTimedOut(logger, sha256, pageNumber);

                return null;
        }

        if (result.ExitCode != 0)
        {
            LogFailed(logger, sha256, pageNumber, result.ExitCode);

            return null;
        }

        // The text ends in a newline that belongs to the program's output and not to the page.
        return result.StandardOutput.TrimEnd();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "tesseract failed to read page {PageNumber} of content {Sha256} with exit code {ExitCode}"
    )]
    private static partial void LogFailed(ILogger logger, string sha256, int pageNumber, int exitCode);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "tesseract ran out of time reading page {PageNumber} of content {Sha256}"
    )]
    private static partial void LogTimedOut(ILogger logger, string sha256, int pageNumber);
}
