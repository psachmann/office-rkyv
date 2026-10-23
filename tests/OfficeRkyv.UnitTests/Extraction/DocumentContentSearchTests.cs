using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// Finding a document by the words in its file, as a client sees it: the scenarios of
/// "A user's documents can be searched by title, file name and text" and "Results are ordered by relevance and
/// limited" in the <c>document-search</c> spec that are about the text, and the scenarios of
/// "Extracted text is searchable once its extraction has completed", "A content is indexed once for all
/// documents that share it" and "What the document is does not change the text" in <c>document-content-index</c>.
/// An upload is searched for as soon as its extraction outcome exists, with no wait beyond that.
/// </summary>
public sealed class DocumentContentSearchTests : ExtractionPipelineTestBase
{
    // At least 20 letters or digits, so a PDF page with them has a text layer that is used.
    private const string Contract = "Kaufvertrag fuer das Haus";

    private async Task<DocumentDto> UploadExtractedAsync(byte[] content, string title, UserDto? user = null)
    {
        var document = await TestDocuments.UploadAsync(
            DocumentsOf(user ?? Alice),
            $"{title}.pdf",
            content,
            title: title
        );
        await Pipeline.WaitForExtractionAsync(document.Sha256);

        return document;
    }

    private async Task<SearchDocumentsResponse> SearchAsync(string query, UserDto? user = null) =>
        await DocumentsOf(user ?? Alice).SearchDocumentsAsync(new SearchDocumentsRequest { Query = query });

    private async Task<string[]> TitlesAsync(string query, UserDto? user = null) =>
        [.. (await SearchAsync(query, user)).Documents.Select(document => document.Title)];

    // ---- what matches ----

    [Test]
    public async Task AWordOnlyInTheTextOfAPdf_FindsTheDocument()
    {
        await UploadExtractedAsync(TestPdfs.WithPages("Zahnarztrechnung fuer Maerz 2026"), "Scan");

        await Assert.That(await TitlesAsync("zahnarzt")).IsEquivalentTo(["Scan"]);
    }

    [Test]
    public async Task AWordOnlyInTheTextOfAScan_FindsTheDocument()
    {
        Ocr.TextOfPage[1] = "Kündigung des Mietvertrags zum 30. Juni";
        await UploadExtractedAsync(TestPdfs.Png(), "Foto");

        await Assert.That(await TitlesAsync("kündigung")).IsEquivalentTo(["Foto"]);
    }

    [Test]
    public async Task OneWordInTheTitle_AndOneInTheText_FindTheDocument()
    {
        await UploadExtractedAsync(TestPdfs.WithPages("Monthly rent of 950 euros"), "Lease");

        await Assert.That(await TitlesAsync("lease rent")).IsEquivalentTo(["Lease"]);
    }

    [Test]
    public async Task WordsOnDifferentPages_FindTheDocument()
    {
        await UploadExtractedAsync(TestPdfs.WithPages(TestPdfs.InvoiceText, "Payable within fourteen days"), "Scan");

        await Assert.That(await TitlesAsync("4711 fourteen")).IsEquivalentTo(["Scan"]);
    }

    [Test]
    public async Task AWordInNoneOfThePlaces_DoesNotFindTheDocument()
    {
        await UploadExtractedAsync(TestPdfs.WithPages("Monthly rent of 950 euros"), "Lease");

        await Assert.That(await TitlesAsync("lease mortgage")).IsEmpty();
    }

    [Test]
    public async Task AFileWithNoText_IsFoundByItsTitleOnly()
    {
        var zip = TestPdfs.Zip();
        await UploadExtractedAsync([.. zip, .. "Kaufvertrag"u8.ToArray()], "Archive");

        await Assert.That(await TitlesAsync("archive")).IsEquivalentTo(["Archive"]);
        await Assert.That(await TitlesAsync("kaufvertrag")).IsEmpty();
    }

    [Test]
    public async Task BeforeTheExtractionHasRun_ADocumentIsFoundByItsTitle()
    {
        Ocr.HoldReads = new TaskCompletionSource();
        var document = await TestDocuments.UploadAsync(
            DocumentsOf(Alice),
            "march.pdf",
            TestPdfs.WithoutText(),
            title: "Invoice March"
        );
        await Ocr.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await Assert.That(await Pipeline.FindExtractionAsync(document.Sha256)).IsNull();
        await Assert.That(await TitlesAsync("march")).IsEquivalentTo(["Invoice March"]);
        Ocr.HoldReads.SetResult();
    }

    [Test]
    public async Task TwoDocumentsWithTheSameFile_AreBothFound()
    {
        var pdf = TestPdfs.WithPages(Contract);
        await UploadExtractedAsync(pdf, "Copy one");
        await UploadExtractedAsync(pdf, "Copy two");

        await Assert.That(await TitlesAsync("kaufvertrag")).IsEquivalentTo(["Copy one", "Copy two"]);
    }

    // ---- the order and the limit ----

    [Test]
    public async Task AMatchInTheTitle_ComesBeforeAMatchInTheText()
    {
        await UploadExtractedAsync(TestPdfs.WithPages(TestPdfs.InvoiceText), "Scan");
        await UploadExtractedAsync(TestPdfs.Zip(), "Invoice for the repair of the garden fence, March");

        await Assert
            .That(await TitlesAsync("invoice"))
            .IsEquivalentTo(
                ["Invoice for the repair of the garden fence, March", "Scan"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching
            );
    }

    [Test]
    public async Task MatchesInTheText_AreListedInTitleOrder()
    {
        var pdf = TestPdfs.WithPages(Contract);
        await UploadExtractedAsync(pdf, "Scan B");
        await UploadExtractedAsync(pdf, "Scan A");

        await Assert
            .That(await TitlesAsync("kaufvertrag"))
            .IsEquivalentTo(["Scan A", "Scan B"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ADocumentMatchedByTitleAndText_IsListedOnce()
    {
        await UploadExtractedAsync(TestPdfs.WithPages(TestPdfs.InvoiceText), "Invoice March");

        await Assert.That(await TitlesAsync("invoice")).IsEquivalentTo(["Invoice March"]);
    }

    [Test]
    public async Task MoreMatchesInTheTextThanTheLimit_AreCutOff_AndSaySo()
    {
        var pdf = TestPdfs.WithPages(Contract);

        for (var number = 1; number <= 51; number++)
        {
            await UploadExtractedAsync(pdf, $"Scan {number:00}");
        }

        var response = await SearchAsync("kaufvertrag");

        await Assert.That(response.Documents.Count).IsEqualTo(50);
        await Assert.That(response.HasMore).IsTrue();
    }

    // ---- searchable when the outcome exists ----

    [Test]
    public async Task TheText_IsSearchableAsSoonAsTheOutcomeExists()
    {
        var document = await TestDocuments.UploadAsync(
            DocumentsOf(Alice),
            "scan.pdf",
            TestPdfs.WithPages("Heizkostenabrechnung 2025"),
            title: "Scan"
        );

        await Pipeline.WaitForExtractionAsync(document.Sha256);

        await Assert.That(await TitlesAsync("heizkosten")).IsEquivalentTo(["Scan"]);
    }

    [Test]
    public async Task AWordInsideALongerWordOfTheText_FindsTheDocument()
    {
        await UploadExtractedAsync(TestPdfs.WithPages("Stromrechnung fuer April"), "Scan");

        await Assert.That(await TitlesAsync("rechnung")).IsEquivalentTo(["Scan"]);
    }

    [Test]
    public async Task APageThatCouldNotBeRead_DoesNotStopTheOthersFromBeingFound()
    {
        Ocr.TextOfPage[1] = "Heizkostenabrechnung";
        Ocr.TextOfPage[3] = "Nachzahlung fällig";
        Ocr.FailOnPages.Add(2);
        await UploadExtractedAsync(TestPdfs.WithoutText(3), "Scan");

        await Assert.That(await TitlesAsync("nachzahlung")).IsEquivalentTo(["Scan"]);
    }

    // ---- one text for all documents that share a file ----

    [Test]
    public async Task TwoUsersWithTheSameFile_EachFindOnlyTheirOwnDocument()
    {
        var bob = await CreateUserAsync("bob@example.com");
        var pdf = TestPdfs.WithPages("Mietvertrag Hauptstrasse");
        await UploadExtractedAsync(pdf, "Alice's lease");
        await UploadExtractedAsync(pdf, "Bob's lease", bob);

        await Assert.That(await TitlesAsync("mietvertrag")).IsEquivalentTo(["Alice's lease"]);
        await Assert.That(await TitlesAsync("mietvertrag", bob)).IsEquivalentTo(["Bob's lease"]);
    }

    [Test]
    public async Task TheSameFileUploadedTwice_IsHeldOnce_AndBothDocumentsAreFound()
    {
        var pdf = TestPdfs.WithPages(Contract);
        await UploadExtractedAsync(pdf, "Copy one");
        await UploadExtractedAsync(pdf, "Copy two");
        await Pipeline.WaitForExtractionsToSettleAsync();

        await Assert.That(await Pipeline.CountContentTextsAsync()).IsEqualTo(1);
        await Assert.That(await TitlesAsync("kaufvertrag")).IsEquivalentTo(["Copy one", "Copy two"]);
    }

    // ---- what the document is does not change the text ----

    [Test]
    public async Task ARenamedDocument_IsStillFoundByItsText()
    {
        var document = await UploadExtractedAsync(TestPdfs.WithPages("Kuendigung vom ersten Mai"), "Scan");

        await DocumentsOf(Alice)
            .RenameDocumentAsync(
                new RenameDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    Title = "Letter",
                }
            );

        await Assert.That(await TitlesAsync("kuendigung")).IsEquivalentTo(["Letter"]);
    }

    [Test]
    public async Task AFiledDocument_IsFoundByItsText_WithItsArchive()
    {
        var document = await UploadExtractedAsync(TestPdfs.WithPages("Kuendigung vom ersten Mai"), "Scan");
        var housing = (
            await new ArchivesService.ArchivesServiceClient(Pipeline.ChannelFor(Alice.Id)).CreateArchiveAsync(
                new CreateArchiveRequest { Name = "Housing" }
            )
        ).Archive;

        await DocumentsOf(Alice)
            .FileDocumentAsync(
                new FileDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    ArchiveId = housing.Id,
                }
            );

        var found = (await SearchAsync("kuendigung")).Documents.Single();
        await Assert.That(found.ArchiveId).IsEqualTo(housing.Id);
    }

    [Test]
    public async Task ADeletedDocument_IsNotFoundByItsText()
    {
        var document = await UploadExtractedAsync(TestPdfs.WithPages("Kuendigung vom ersten Mai"), "Scan");

        await DocumentsOf(Alice)
            .DeleteDocumentAsync(new DeleteDocumentRequest { DocumentId = document.Id, Version = document.Version });

        await Assert.That(await TitlesAsync("kuendigung")).IsEmpty();
    }

    [Test]
    public async Task ARenameWithTheUploadsVersion_SucceedsOnceTheTextIsSearchable()
    {
        var document = await UploadExtractedAsync(TestPdfs.WithPages(Contract), "Scan");
        await Assert.That(await TitlesAsync("kaufvertrag")).IsEquivalentTo(["Scan"]);

        var renamed = await DocumentsOf(Alice)
            .RenameDocumentAsync(
                new RenameDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    Title = "Contract",
                }
            );

        await Assert.That(renamed.Document.Title).IsEqualTo("Contract");
    }
}
