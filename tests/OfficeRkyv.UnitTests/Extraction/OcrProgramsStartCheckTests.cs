using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>The start check's result for each combination of installed programs and languages, with no process started.</summary>
public sealed class OcrProgramsStartCheckTests
{
    private const string TesseractVersion = "tesseract 5.5.3\n leptonica-1.87.0\n  libgif 6.1.3";
    private const string PdftoppmVersion = "pdftoppm version 26.06.0\nCopyright 2005-2026 The Poppler Developers";

    private static readonly ProgramResult NotInstalled = new(ProgramOutcome.NotStarted);

    private static ProgramResult Ok(string output = "", string error = "") =>
        new(ProgramOutcome.Exited, 0, output, error);

    private static string Languages(params string[] codes) =>
        "List of available languages in \"/usr/share/tessdata/\" ("
        + codes.Length
        + "):\n"
        + string.Concat(codes.Select(code => code + "\n"));

    private static ProgramResult TesseractAnswers(IReadOnlyList<string> arguments, params string[] installed) =>
        arguments[0] == "--version" ? Ok(TesseractVersion) : Ok(Languages(installed));

    private static async Task<List<(LogLevel Level, string Message)>> RunAsync(
        Func<string, IReadOnlyList<string>, ProgramResult> answer,
        string languages = "deu+eng"
    )
    {
        var logger = new CapturingLogger<OcrProgramsStartCheck>();
        var options = Options.Create(new TextExtractionOptions { Languages = languages });

        await new OcrProgramsStartCheck(new FakeProgramRunner(answer), options, logger).StartAsync(
            CancellationToken.None
        );

        return logger.Entries;
    }

    [Test]
    public async Task BothProgramsAndAllLanguages_LogOneInformationWithTheVersionsAndLanguages()
    {
        var entries = await RunAsync(
            (program, arguments) =>
                program == "tesseract" ? TesseractAnswers(arguments, "deu", "eng", "osd") : Ok(error: PdftoppmVersion)
        );

        var entry = entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Information);
        await Assert.That(entry.Message).Contains("tesseract 5.5.3");
        await Assert.That(entry.Message).Contains("pdftoppm version 26.06.0");
        await Assert.That(entry.Message).Contains("deu+eng");
    }

    [Test]
    public async Task ATesseractThatIsMissing_LogsOneWarning_ThatNamesIt()
    {
        var entries = await RunAsync(
            (program, _) => program == "tesseract" ? NotInstalled : Ok(error: PdftoppmVersion)
        );

        var entry = entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).Contains("tesseract was not found");
        await Assert.That(entry.Message).Contains("Scanned pages will not be read");
        await Assert.That(entry.Message).DoesNotContain("pdftoppm");
    }

    [Test]
    public async Task APdftoppmThatIsMissing_LogsOneWarning_ThatNamesIt()
    {
        var entries = await RunAsync(
            (program, arguments) => program == "tesseract" ? TesseractAnswers(arguments, "deu", "eng") : NotInstalled
        );

        var entry = entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).Contains("pdftoppm was not found");
        await Assert.That(entry.Message).DoesNotContain("tesseract was not found");
    }

    [Test]
    public async Task BothMissing_LogOneWarning_ThatNamesBoth()
    {
        var entries = await RunAsync((_, _) => NotInstalled);

        var entry = entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).Contains("tesseract was not found");
        await Assert.That(entry.Message).Contains("pdftoppm was not found");
    }

    [Test]
    public async Task ALanguageThatIsNotInstalled_LogsOneWarning_ThatNamesIt()
    {
        var entries = await RunAsync(
            (program, arguments) =>
                program == "tesseract" ? TesseractAnswers(arguments, "eng", "osd") : Ok(error: PdftoppmVersion)
        );

        var entry = entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).Contains("language data is missing for deu");
        await Assert.That(entry.Message).DoesNotContain("eng,");
    }

    [Test]
    public async Task AProgramThatFails_LogsOneWarning_WithItsExitCode()
    {
        var entries = await RunAsync(
            (program, arguments) =>
                program == "tesseract"
                    ? TesseractAnswers(arguments, "deu", "eng")
                    : new ProgramResult(ProgramOutcome.Exited, 139)
        );

        var entry = entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).Contains("exit code 139");
    }

    [Test]
    public async Task OnlyTheConfiguredLanguages_AreRequired()
    {
        var entries = await RunAsync(
            (program, arguments) =>
                program == "tesseract" ? TesseractAnswers(arguments, "eng") : Ok(error: PdftoppmVersion),
            languages: "eng"
        );

        await Assert.That(entries.Single().Level).IsEqualTo(LogLevel.Information);
    }

    [Test]
    public async Task ACheckThatThrows_NeverStopsTheHost_AndLogsOneWarning()
    {
        var logger = new CapturingLogger<OcrProgramsStartCheck>();
        var runner = new ThrowingRunner();

        await new OcrProgramsStartCheck(runner, Options.Create(new TextExtractionOptions()), logger).StartAsync(
            CancellationToken.None
        );

        await Assert.That(logger.Entries.Single().Level).IsEqualTo(LogLevel.Warning);
    }

    private sealed class ThrowingRunner : IProgramRunner
    {
        public Task<ProgramResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeLimit,
            CancellationToken cancellation = default
        ) => throw new InvalidOperationException("The runner broke.");
    }
}
