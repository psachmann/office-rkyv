using System.Globalization;
using Microsoft.Extensions.Options;

namespace OfficeRkyv.Api.Shared.Extraction;

/// <summary>Renders a page with poppler's <c>pdftoppm</c>, at 300 DPI, which is what OCR reads well.</summary>
internal sealed partial class PdftoppmPageRenderer(
    IProgramRunner runner,
    IOptions<TextExtractionOptions> options,
    ILogger<PdftoppmPageRenderer> logger
) : IPageRenderer
{
    private const string Dpi = "300";

    public async Task<string?> RenderAsync(
        string sha256,
        string pdfPath,
        int pageNumber,
        string imagePathWithoutExtension,
        CancellationToken cancellation = default
    )
    {
        var page = pageNumber.ToString(CultureInfo.InvariantCulture);
        var result = await runner.RunAsync(
            options.Value.PdftoppmPath,
            ["-f", page, "-l", page, "-r", Dpi, "-png", "-singlefile", pdfPath, imagePathWithoutExtension],
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

        var image = imagePathWithoutExtension + ".png";

        if (result.ExitCode != 0 || !File.Exists(image))
        {
            LogFailed(logger, sha256, pageNumber, result.ExitCode);

            return null;
        }

        return image;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "pdftoppm failed to render page {PageNumber} of content {Sha256} with exit code {ExitCode}"
    )]
    private static partial void LogFailed(ILogger logger, string sha256, int pageNumber, int exitCode);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "pdftoppm ran out of time rendering page {PageNumber} of content {Sha256}"
    )]
    private static partial void LogTimedOut(ILogger logger, string sha256, int pageNumber);
}
