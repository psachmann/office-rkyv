using Fisher;
using Google.Rpc;
using Grpc.Core;
using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Api.Features.Documents.Handlers;
using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Shared;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;
using OfficeRkyv.UnitTests.Grpc;
using TUnit.Assertions.Enums;

namespace OfficeRkyv.UnitTests.Documents;

/// <summary>
/// Searching a user's documents by title and file name, as a client sees it: through the real service, the
/// real full-text index and the real error mapping.
/// </summary>
public sealed class DocumentSearchTests : IAsyncDisposable
{
    private PipelineHost pipeline = null!;

    // Every call is made as a user: the API knows the owner from the caller, not from the request.
    private DocumentsService.DocumentsServiceClient DocumentsOf(UserDto user) => new(pipeline.ChannelFor(user.Id));

    private UsersService.UsersServiceClient Users => new(pipeline.Channel);

    private ArchivesService.ArchivesServiceClient ArchivesOf(UserDto user) => new(pipeline.ChannelFor(user.Id));

    [Before(Test)]
    public async Task SetUp() => pipeline = await PipelineHost.StartAsync();

    private async Task<UserDto> CreateUser(string email = "alice@example.com") =>
        (await Users.CreateUserAsync(new CreateUserRequest { Email = email, DisplayName = email })).User;

    // A document made by an upload of a small file, so its title and file name are chosen separately.
    private Task<DocumentDto> Upload(UserDto owner, string title, string? fileName = null) =>
        TestDocuments.UploadAsync(
            DocumentsOf(owner),
            fileName ?? $"{title}.pdf",
            TestDocuments.Bytes(10),
            title: title
        );

    private async Task<SearchDocumentsResponse> Search(UserDto user, string query) =>
        await DocumentsOf(user).SearchDocumentsAsync(new SearchDocumentsRequest { Query = query });

    private async Task<string[]> Titles(UserDto owner, string query) =>
        [.. (await Search(owner, query)).Documents.Select(document => document.Title)];

    private static (string Field, string Reason) Violation(RpcException failure) =>
        GrpcFailure.Detail<BadRequest>(failure).FieldViolations.Select(v => (v.Field, v.Reason)).Single();

    // ---- what matches ----

    [Test]
    public async Task AWord_MatchesInsideALongerWord()
    {
        var alice = await CreateUser();
        await Upload(alice, "Stromrechnung März");

        await Assert.That(await Titles(alice, "rechnung")).IsEquivalentTo(["Stromrechnung März"]);
    }

    [Test]
    public async Task TheCaseOfTheQuery_IsIgnored()
    {
        var alice = await CreateUser();
        await Upload(alice, "Invoice March");

        await Assert.That(await Titles(alice, "INVOICE")).IsEquivalentTo(["Invoice March"]);
    }

    [Test]
    public async Task EveryWord_HasToMatch()
    {
        var alice = await CreateUser();
        await Upload(alice, "Invoice March");
        await Upload(alice, "Invoice April");

        await Assert.That(await Titles(alice, "invoice april")).IsEquivalentTo(["Invoice April"]);
    }

    [Test]
    public async Task TheWords_MatchInAnyOrder()
    {
        var alice = await CreateUser();
        await Upload(alice, "Invoice March");

        await Assert.That(await Titles(alice, "march invoice")).IsEquivalentTo(["Invoice March"]);
    }

    [Test]
    public async Task AWord_MatchesTheFileName()
    {
        var alice = await CreateUser();
        await Upload(alice, "Lease", "mietvertrag-2026.pdf");

        await Assert.That(await Titles(alice, "mietvertrag")).IsEquivalentTo(["Lease"]);
    }

    [Test]
    public async Task OneWordInTheTitle_AndOneInTheFileName_Match()
    {
        var alice = await CreateUser();
        await Upload(alice, "Lease", "mietvertrag-2026.pdf");

        await Assert.That(await Titles(alice, "lease 2026")).IsEquivalentTo(["Lease"]);
    }

    [Test]
    public async Task AWordInNoTitleOrFileName_GivesAnEmptyResult_NotAFailure()
    {
        var alice = await CreateUser();
        await Upload(alice, "Invoice March");

        var response = await Search(alice, "garden");

        await Assert.That(response.Documents).IsEmpty();
        await Assert.That(response.HasMore).IsFalse();
    }

    // ---- whose documents ----

    [Test]
    public async Task ASearch_CoversTheInboxAndEveryArchive()
    {
        var alice = await CreateUser();
        var taxes = (
            await ArchivesOf(alice).CreateArchiveAsync(new CreateArchiveRequest { Name = "Taxes 2026" })
        ).Archive;
        await Upload(alice, "Invoice March");
        var april = await Upload(alice, "Invoice April");
        await DocumentsOf(alice)
            .FileDocumentAsync(
                new FileDocumentRequest
                {
                    DocumentId = april.Id,
                    Version = april.Version,
                    ArchiveId = taxes.Id,
                }
            );

        var documents = (await Search(alice, "invoice")).Documents.ToDictionary(document => document.Title);

        await Assert.That(documents.Keys).IsEquivalentTo(["Invoice March", "Invoice April"]);
        await Assert.That(documents["Invoice April"].ArchiveId).IsEqualTo(taxes.Id);
        await Assert.That(documents["Invoice March"].HasArchiveId).IsFalse();
    }

    [Test]
    public async Task ASearch_NeverReturnsAnotherUsersDocuments()
    {
        var alice = await CreateUser();
        var bob = await CreateUser("bob@example.com");
        await Upload(alice, "Invoice");
        await Upload(bob, "Invoice");

        var response = await Search(alice, "invoice");

        await Assert.That(response.Documents.Select(document => document.OwnerId)).IsEquivalentTo([alice.Id]);
    }

    [Test]
    public async Task ADeletedDocument_IsNotFound()
    {
        var alice = await CreateUser();
        var march = await Upload(alice, "Invoice March");
        await Upload(alice, "Invoice April");
        await DocumentsOf(alice)
            .DeleteDocumentAsync(new DeleteDocumentRequest { DocumentId = march.Id, Version = march.Version });

        await Assert.That(await Titles(alice, "invoice")).IsEquivalentTo(["Invoice April"]);
    }

    [Test]
    public async Task ACallerWhoIsNoUser_IsRefusedAsUnauthenticated()
    {
        var alice = await CreateUser();
        await Upload(alice, "Invoice");
        var nobody = new UserDto { Id = Guid.NewGuid().ToString() };

        var failure = await GrpcFailure.Of(() => Search(nobody, "invoice"));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.Unauthenticated);
    }

    // ---- staying current ----

    [Test]
    public async Task ADocument_IsFoundAsSoonAsItsUploadHasAnswered()
    {
        var alice = await CreateUser();
        await Upload(alice, "Invoice March", "Invoice March.pdf");

        await Assert.That(await Titles(alice, "march")).IsEquivalentTo(["Invoice March"]);
    }

    [Test]
    public async Task ARenamedDocument_IsFoundByItsNewTitle_AndNotByTheOldOne()
    {
        var alice = await CreateUser();
        // The file name stays as it was, so it must not contain the word searched for as the old title.
        var document = await Upload(alice, "Invoice March", "scan0001.pdf");

        await DocumentsOf(alice)
            .RenameDocumentAsync(
                new RenameDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    Title = "Electricity bill",
                }
            );

        await Assert.That(await Titles(alice, "invoice")).IsEmpty();
        await Assert.That(await Titles(alice, "electricity")).IsEquivalentTo(["Electricity bill"]);
    }

    [Test]
    public async Task ADocumentFromBeforeSearch_IsFound_OnceTheApplicationStartsWithTheIndex()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.db");
        var ownerId = Guid.NewGuid();

        // A store as the version before search configured it: snapshots, and no full-text index.
        using (
            var before = DocumentStore.For(options =>
            {
                options.Connection($"Data Source={path}");
                options.AddAggregateSnapshots();
            })
        )
        {
            await before.ApplyAllConfiguredChangesToDatabaseAsync();
            await using var session = before.LightweightSession();
            session.Events.StartStream<User>(
                ownerId,
                new UserCreated("old@example.com", "old@example.com", "Old user")
            );
            session.Events.StartStream<Document>(
                Guid.NewGuid(),
                new DocumentCreated(
                    ownerId,
                    "Stromrechnung März",
                    "scan.pdf",
                    "application/pdf",
                    3,
                    new string('0', 64)
                )
            );
            await session.SaveChangesAsync();
        }

        await using var restarted = await PipelineHost.StartAsync(databasePath: path);
        var response = await new DocumentsService.DocumentsServiceClient(
            restarted.ChannelFor(ownerId)
        ).SearchDocumentsAsync(new SearchDocumentsRequest { Query = "rechnung" });

        await Assert.That(response.Documents.Select(document => document.Title)).IsEquivalentTo(["Stromrechnung März"]);
    }

    // ---- order and limit ----

    [Test]
    public async Task MoreMatchesThanTheLimit_AreCutOff_AndSaySo()
    {
        var alice = await CreateUser();

        for (var i = 0; i < SearchDocumentsHandler.Limit + 1; i++)
        {
            await Upload(alice, $"Invoice {i:000}");
        }

        var response = await Search(alice, "invoice");

        await Assert.That(response.Documents.Count).IsEqualTo(SearchDocumentsHandler.Limit);
        await Assert.That(response.HasMore).IsTrue();
    }

    [Test]
    public async Task FewerMatchesThanTheLimit_AreAllReturned_WithoutAMoreFlag()
    {
        var alice = await CreateUser();

        for (var i = 0; i < 3; i++)
        {
            await Upload(alice, $"Invoice {i}");
        }

        var response = await Search(alice, "invoice");

        await Assert.That(response.Documents.Count).IsEqualTo(3);
        await Assert.That(response.HasMore).IsFalse();
    }

    [Test]
    public async Task ACloserMatch_ComesFirst()
    {
        var alice = await CreateUser();
        await Upload(alice, "Annual invoice for the repair of the garden fence, March");
        await Upload(alice, "Invoice");

        await Assert
            .That(await Titles(alice, "invoice"))
            .IsEquivalentTo(
                ["Invoice", "Annual invoice for the repair of the garden fence, March"],
                CollectionOrdering.Matching
            );
    }

    // ---- refused queries ----

    [Test]
    [Arguments("ab")]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("a- b.")]
    public async Task AQueryWithNoWordOfThreeCharacters_IsTooShort(string query)
    {
        var alice = await CreateUser();

        var failure = await GrpcFailure.Of(() => Search(alice, query));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert.That(Violation(failure)).IsEqualTo(("query", ViolationCodes.TooShort));
    }

    [Test]
    public async Task AQueryOverTheLimit_IsTooLong_AndOneAtTheLimitIsAccepted()
    {
        var alice = await CreateUser();

        var failure = await GrpcFailure.Of(() => Search(alice, new string('a', SearchTerms.MaxQueryLength + 1)));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert.That(Violation(failure)).IsEqualTo(("query", ViolationCodes.TooLong));
        await Assert.That((await Search(alice, new string('a', SearchTerms.MaxQueryLength))).Documents).IsEmpty();
    }

    [Test]
    public async Task ShortWords_NextToALongOne_AreIgnored()
    {
        var alice = await CreateUser();
        await Upload(alice, "Invoice 4711");

        await Assert.That(await Titles(alice, "an 4711")).IsEquivalentTo(["Invoice 4711"]);
    }

    [Test]
    public async Task SearchSyntax_IsTakenLiterally()
    {
        var alice = await CreateUser();
        await Upload(alice, "Invoice March");

        // AND is a word of three characters here, so it has to be in the title; as an operator it would match.
        await Assert.That(await Titles(alice, "invoice AND march")).IsEmpty();
        // OR and "* are too short to be words, so they are ignored and the rest is searched.
        await Assert.That(await Titles(alice, "invoice OR \"*")).IsEquivalentTo(["Invoice March"]);
        // A quote inside a word cannot end the quoted string it is searched in.
        await Assert.That(await Titles(alice, "inv\"oice")).IsEmpty();
    }

    public async ValueTask DisposeAsync() => await pipeline.DisposeAsync();
}
