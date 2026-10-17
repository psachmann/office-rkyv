using OfficeRkyv.Api.Shared.Extraction;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// The one test that runs the real OCR programs, for the wiring that the fakes cannot show: the argument lists,
/// the files the programs read and write, and their output. It builds its scan itself: a PDF with a text page is
/// rendered by <c>pdftoppm</c>, and the picture is the only content of a second PDF, which has no text layer. It
/// is skipped when either program is not on <c>PATH</c>; the dev shell has both.
/// </summary>
public sealed class RealProgramsPipelineTests
{
    [Test]
    public async Task AScannedPdf_IsReadByTheRealPrograms()
    {
        if (OnPath("tesseract") is null || OnPath("pdftoppm") is null)
        {
            Skip.Test("tesseract and pdftoppm are not both on PATH; the dev shell (nix develop) provides them.");
        }

        var scan = await ScanOfAsync(TestPdfs.WithPages(TestPdfs.InvoiceText));

        await using var host = await PipelineHost.StartAsync();
        var alice = (
            await new UsersService.UsersServiceClient(host.Channel).CreateUserAsync(
                new CreateUserRequest { Email = "alice@example.com", DisplayName = "Alice" }
            )
        ).User;
        var document = await TestDocuments.UploadAsync(
            new DocumentsService.DocumentsServiceClient(host.ChannelFor(alice.Id)),
            "scan.pdf",
            scan
        );

        var outcome = await host.WaitForExtractionAsync(document.Sha256, TimeSpan.FromMinutes(2));

        await Assert.That(outcome.Outcome).IsEqualTo(TextExtractionOutcome.Extracted);
        await Assert.That(outcome.PageCount).IsEqualTo(1);
        await Assert.That(outcome.OcrPageCount).IsEqualTo(1);
        await Assert.That(outcome.UnreadPageCount).IsEqualTo(0);
        var pages = await host.Texts.ReadPagesAsync(document.Sha256);
        await Assert.That(pages![0]).Contains("Invoice");
        await Assert.That(pages[0]).Contains("4711");
        await Assert.That(pages[0]).Contains("March");
        // The page image is deleted once it is read, and no half-written file is left.
        await Assert.That(Directory.EnumerateFiles(Path.Combine(host.BlobDirectory, "tmp"))).IsEmpty();
    }

    /// <summary>Renders the PDF's only page with <c>pdftoppm</c> and returns a PDF that holds just that picture.</summary>
    private static async Task<byte[]> ScanOfAsync(byte[] textPdf)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.scan");
        Directory.CreateDirectory(directory);

        try
        {
            var pdfPath = Path.Combine(directory, "text.pdf");
            await File.WriteAllBytesAsync(pdfPath, textPdf);

            var rendered = await new ProgramRunner().RunAsync(
                "pdftoppm",
                ["-r", "200", "-png", "-singlefile", pdfPath, Path.Combine(directory, "page")],
                TimeSpan.FromSeconds(60)
            );
            await Assert.That(rendered.Succeeded).IsTrue();

            return TestPdfs.ScanOf(await File.ReadAllBytesAsync(Path.Combine(directory, "page.png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string? OnPath(string program) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, program))
            .FirstOrDefault(File.Exists);
}
