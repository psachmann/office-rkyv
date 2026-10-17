using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// The argument lists of the two OCR programs, and how a failed, timed-out or missing program becomes an unread
/// page. A fake runner stands in for the processes; the real ones are covered by the real-program pipeline test.
/// </summary>
public sealed class OcrProgramTests : IDisposable
{
    private static readonly string Hash = new('a', 64);
    private const string LeakedOutput = "SECRET-WORDS-FROM-THE-DOCUMENT";

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.ocr");

    private readonly IOptions<TextExtractionOptions> options = Options.Create(
        new TextExtractionOptions
        {
            Languages = "deu+eng",
            TesseractPath = "my-tesseract",
            PdftoppmPath = "my-pdftoppm",
            TimeLimit = TimeSpan.FromSeconds(7),
        }
    );

    public OcrProgramTests() => Directory.CreateDirectory(directory);

    private string Prefix => Path.Combine(directory, "page");

    private (
        PdftoppmPageRenderer Renderer,
        FakeProgramRunner Runner,
        CapturingLogger<PdftoppmPageRenderer> Logger
    ) Renderer(Func<string, IReadOnlyList<string>, ProgramResult> answer)
    {
        var runner = new FakeProgramRunner(answer);
        var logger = new CapturingLogger<PdftoppmPageRenderer>();

        return (new PdftoppmPageRenderer(runner, options, logger), runner, logger);
    }

    private (
        TesseractImageReader Reader,
        FakeProgramRunner Runner,
        CapturingLogger<TesseractImageReader> Logger
    ) Reader(Func<string, IReadOnlyList<string>, ProgramResult> answer)
    {
        var runner = new FakeProgramRunner(answer);
        var logger = new CapturingLogger<TesseractImageReader>();

        return (new TesseractImageReader(runner, options, logger), runner, logger);
    }

    private static ProgramResult Exited(int code, string output = "", string error = "") =>
        new(ProgramOutcome.Exited, code, output, error);

    // ---- rendering ----

    [Test]
    public async Task APage_IsRenderedWithPdftoppm_At300Dpi_ToAPngWithoutShell()
    {
        var (renderer, runner, logger) = Renderer(
            (_, _) =>
            {
                File.WriteAllText(Prefix + ".png", "png");

                return Exited(0);
            }
        );

        var image = await renderer.RenderAsync(Hash, "/blobs/aa/pdf", 3, Prefix);

        await Assert.That(image).IsEqualTo(Prefix + ".png");
        var call = runner.Calls.Single();
        await Assert.That(call.FileName).IsEqualTo("my-pdftoppm");
        await Assert
            .That(call.Arguments)
            .IsEquivalentTo(["-f", "3", "-l", "3", "-r", "300", "-png", "-singlefile", "/blobs/aa/pdf", Prefix]);
        await Assert.That(call.TimeLimit).IsEqualTo(TimeSpan.FromSeconds(7));
        await Assert.That(logger.Entries).IsEmpty();
    }

    [Test]
    public async Task ARenderThatExitsWithAnError_IsUnread_AndOneWarningNamesTheContentThePageAndTheCode()
    {
        var (renderer, _, logger) = Renderer((_, _) => Exited(1, LeakedOutput, LeakedOutput));

        var image = await renderer.RenderAsync(Hash, "/blobs/aa/pdf", 4, Prefix);

        await Assert.That(image).IsNull();
        var entry = logger.Entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).Contains(Hash);
        await Assert.That(entry.Message).Contains("4");
        await Assert.That(entry.Message).Contains("exit code 1");
        await Assert.That(entry.Message).DoesNotContain(LeakedOutput);
    }

    [Test]
    public async Task ARenderThatSucceedsButWritesNoImage_IsUnread()
    {
        var (renderer, _, logger) = Renderer((_, _) => Exited(0));

        await Assert.That(await renderer.RenderAsync(Hash, "/blobs/aa/pdf", 1, Prefix)).IsNull();
        await Assert.That(logger.Entries.Single().Level).IsEqualTo(LogLevel.Warning);
    }

    [Test]
    public async Task ARenderOverTheTimeLimit_IsUnread_AndOneWarningSaysSo()
    {
        var (renderer, _, logger) = Renderer((_, _) => new ProgramResult(ProgramOutcome.TimedOut));

        await Assert.That(await renderer.RenderAsync(Hash, "/blobs/aa/pdf", 2, Prefix)).IsNull();

        var entry = logger.Entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).Contains(Hash);
        await Assert.That(entry.Message).Contains("2");
    }

    [Test]
    public async Task APdftoppmThatIsNotInstalled_IsUnread_WithoutAWarningPerPage()
    {
        var (renderer, _, logger) = Renderer((_, _) => new ProgramResult(ProgramOutcome.NotStarted));

        await Assert.That(await renderer.RenderAsync(Hash, "/blobs/aa/pdf", 1, Prefix)).IsNull();
        await Assert.That(logger.Entries).IsEmpty();
    }

    // ---- reading ----

    [Test]
    public async Task AnImage_IsReadWithTesseract_InTheConfiguredLanguages_ToStandardOutput()
    {
        var (reader, runner, logger) = Reader((_, _) => Exited(0, "Rechnung 4711\n"));

        var text = await reader.ReadAsync(Hash, 1, "/blobs/aa/image");

        await Assert.That(text).IsEqualTo("Rechnung 4711");
        var call = runner.Calls.Single();
        await Assert.That(call.FileName).IsEqualTo("my-tesseract");
        await Assert.That(call.Arguments).IsEquivalentTo(["/blobs/aa/image", "stdout", "-l", "deu+eng"]);
        await Assert.That(call.TimeLimit).IsEqualTo(TimeSpan.FromSeconds(7));
        await Assert.That(logger.Entries).IsEmpty();
    }

    [Test]
    public async Task AnImageWithoutText_IsReadAsEmptyText_NotAsUnread()
    {
        var (reader, _, logger) = Reader((_, _) => Exited(0, ""));

        await Assert.That(await reader.ReadAsync(Hash, 1, "/blobs/aa/image")).IsEqualTo("");
        await Assert.That(logger.Entries).IsEmpty();
    }

    [Test]
    public async Task AReadThatExitsWithAnError_IsUnread_AndOneWarningNamesTheContentThePageAndTheCode()
    {
        var (reader, _, logger) = Reader((_, _) => Exited(1, LeakedOutput, LeakedOutput));

        var text = await reader.ReadAsync(Hash, 5, "/blobs/aa/image");

        await Assert.That(text).IsNull();
        var entry = logger.Entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).Contains(Hash);
        await Assert.That(entry.Message).Contains("5");
        await Assert.That(entry.Message).Contains("exit code 1");
        await Assert.That(entry.Message).DoesNotContain(LeakedOutput);
    }

    [Test]
    public async Task AReadOverTheTimeLimit_IsUnread_AndOneWarningSaysSo()
    {
        var (reader, _, logger) = Reader(
            (_, _) => new ProgramResult(ProgramOutcome.TimedOut, StandardOutput: LeakedOutput)
        );

        await Assert.That(await reader.ReadAsync(Hash, 2, "/blobs/aa/image")).IsNull();

        var entry = logger.Entries.Single();
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).Contains(Hash);
        await Assert.That(entry.Message).DoesNotContain(LeakedOutput);
    }

    [Test]
    public async Task ATesseractThatIsNotInstalled_IsUnread_WithoutAWarningPerPage()
    {
        var (reader, _, logger) = Reader((_, _) => new ProgramResult(ProgramOutcome.NotStarted));

        await Assert.That(await reader.ReadAsync(Hash, 1, "/blobs/aa/image")).IsNull();
        await Assert.That(logger.Entries).IsEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
