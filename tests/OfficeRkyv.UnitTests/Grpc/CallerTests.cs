using System.Security.Claims;
using Grpc.Core;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;

namespace OfficeRkyv.UnitTests.Grpc;

/// <summary>
/// Who is calling, as the API decides it: the archive and document services answer a call without an accepted
/// caller as <c>UNAUTHENTICATED</c> (spec <c>api-authorization</c>), through the real service and error mapping.
/// </summary>
public sealed class CallerTests : IAsyncDisposable
{
    private PipelineHost pipeline = null!;

    private UsersService.UsersServiceClient Users => new(pipeline.Channel);

    private ArchivesService.ArchivesServiceClient ArchivesOf(UserDto user) => new(pipeline.ChannelFor(user.Id));

    [Before(Test)]
    public async Task SetUp() => pipeline = await PipelineHost.StartAsync();

    private async Task<UserDto> CreateUser(string email = "alice@example.com") =>
        (await Users.CreateUserAsync(new CreateUserRequest { Email = email, DisplayName = email })).User;

    private ArchivesService.ArchivesServiceClient Archives => new(pipeline.Channel);

    private DocumentsService.DocumentsServiceClient Documents => new(pipeline.Channel);

    private static async Task AssertUnauthenticated(RpcException failure)
    {
        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.Unauthenticated);
        await Assert.That(failure.Status.Detail).IsEqualTo("The call needs a signed-in user.");
        await Assert.That(failure.GetRpcStatus()!.Details).IsEmpty();
    }

    // ---- a call without an accepted caller ----

    [Test]
    public async Task NoToken_IsUnauthenticated()
    {
        var failure = await GrpcFailure.Of(async () => await Documents.ListInboxAsync(new ListInboxRequest()));

        await AssertUnauthenticated(failure);
    }

    [Test]
    public async Task ATokenSignedWithAnotherKey_IsUnauthenticated_AndCreatesNothing()
    {
        var alice = await CreateUser();
        var options = PipelineHost.CallOptionsFor(Guid.Parse(alice.Id), PipelineHost.OtherKey);

        var failure = await GrpcFailure.Of(async () =>
            await Archives.CreateArchiveAsync(new CreateArchiveRequest { Name = "Taxes" }, options)
        );

        await AssertUnauthenticated(failure);
        await Assert.That((await ArchivesOf(alice).ListArchivesAsync(new ListArchivesRequest())).Archives).IsEmpty();
    }

    [Test]
    public async Task ATokenForAnotherAudience_IsUnauthenticated()
    {
        var alice = await CreateUser();
        var token = new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = CallerTokens.Issuer,
                Audience = "some-other-api",
                Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, alice.Id)]),
                Expires = DateTime.UtcNow.AddMinutes(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(PipelineHost.CallerKey),
                    SecurityAlgorithms.HmacSha256
                ),
            }
        );

        var failure = await GrpcFailure.Of(async () =>
            await Archives.ListArchivesAsync(
                new ListArchivesRequest(),
                new CallOptions(new Metadata { { "authorization", $"Bearer {token}" } })
            )
        );

        await AssertUnauthenticated(failure);
    }

    [Test]
    public async Task ATokenFromAnotherIssuer_IsUnauthenticated()
    {
        var alice = await CreateUser();
        var token = new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = "someone-else",
                Audience = CallerTokens.Audience,
                Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, alice.Id)]),
                Expires = DateTime.UtcNow.AddMinutes(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(PipelineHost.CallerKey),
                    SecurityAlgorithms.HmacSha256
                ),
            }
        );

        var failure = await GrpcFailure.Of(async () =>
            await Archives.ListArchivesAsync(
                new ListArchivesRequest(),
                new CallOptions(new Metadata { { "authorization", $"Bearer {token}" } })
            )
        );

        await AssertUnauthenticated(failure);
    }

    [Test]
    public async Task AnExpiredToken_IsUnauthenticated()
    {
        var alice = await CreateUser();
        // Past the five minutes of clock skew the validation allows.
        var options = PipelineHost.CallOptionsFor(Guid.Parse(alice.Id), expires: DateTimeOffset.UtcNow.AddMinutes(-10));

        var failure = await GrpcFailure.Of(async () =>
            await Archives.ListArchivesAsync(new ListArchivesRequest(), options)
        );

        await AssertUnauthenticated(failure);
    }

    [Test]
    public async Task ATokenThatIsNoToken_IsUnauthenticated()
    {
        var failure = await GrpcFailure.Of(async () =>
            await Archives.ListArchivesAsync(
                new ListArchivesRequest(),
                new CallOptions(new Metadata { { "authorization", "Bearer not-a-token" } })
            )
        );

        await AssertUnauthenticated(failure);
    }

    [Test]
    public async Task ATokenForAUserWhoWasNeverCreated_IsUnauthenticated()
    {
        var failure = await GrpcFailure.Of(async () =>
            await Archives.ListArchivesAsync(new ListArchivesRequest(), PipelineHost.CallOptionsFor(Guid.NewGuid()))
        );

        await AssertUnauthenticated(failure);
    }

    [Test]
    public async Task ATokenForADeletedUser_IsUnauthenticated_AndCreatesNothing()
    {
        var alice = await CreateUser();
        var asAlice = ArchivesOf(alice);
        await asAlice.CreateArchiveAsync(new CreateArchiveRequest { Name = "Taxes" });
        await Users.DeleteUserAsync(
            new DeleteUserRequest { UserId = alice.Id, Version = alice.Version },
            PipelineHost.CallOptionsFor(Guid.Parse(alice.Id))
        );

        var failure = await GrpcFailure.Of(async () =>
            await TestDocuments.UploadAsync(
                new DocumentsService.DocumentsServiceClient(pipeline.ChannelFor(alice.Id)),
                "a.pdf",
                TestDocuments.Bytes(10)
            )
        );

        await AssertUnauthenticated(failure);
        await Assert.That(Directory.EnumerateFiles(pipeline.BlobDirectory, "*", SearchOption.AllDirectories)).IsEmpty();
    }

    [Test]
    public async Task AnUploadWithoutAToken_StoresNothing()
    {
        var failure = await GrpcFailure.Of(async () =>
            await TestDocuments.UploadAsync(Documents, "a.pdf", TestDocuments.Bytes(500))
        );

        await AssertUnauthenticated(failure);
        await Assert.That(Directory.EnumerateFiles(pipeline.BlobDirectory, "*", SearchOption.AllDirectories)).IsEmpty();
    }

    [Test]
    public async Task AStreamingCallWithoutAToken_EndsAsUnauthenticated_WithNoContent()
    {
        var alice = await CreateUser();
        var document = await TestDocuments.UploadAsync(
            new DocumentsService.DocumentsServiceClient(pipeline.ChannelFor(alice.Id)),
            "a.pdf",
            TestDocuments.Bytes(100)
        );

        var failure = await GrpcFailure.Of(async () => await TestDocuments.DownloadAsync(Documents, document.Id));

        await AssertUnauthenticated(failure);
    }

    [Test]
    public async Task EveryArchiveAndDocumentMethod_NeedsACaller()
    {
        var any = Guid.NewGuid().ToString();
        var calls = new Dictionary<string, Func<Task>>
        {
            ["ListArchives"] = async () => await Archives.ListArchivesAsync(new ListArchivesRequest()),
            ["CreateArchive"] = async () => await Archives.CreateArchiveAsync(new CreateArchiveRequest { Name = "x" }),
            ["RenameArchive"] = async () =>
                await Archives.RenameArchiveAsync(new RenameArchiveRequest { ArchiveId = any, Name = "x" }),
            ["DeleteArchive"] = async () =>
                await Archives.DeleteArchiveAsync(new DeleteArchiveRequest { ArchiveId = any }),
            ["ListInbox"] = async () => await Documents.ListInboxAsync(new ListInboxRequest()),
            ["ListArchiveDocuments"] = async () =>
                await Documents.ListArchiveDocumentsAsync(new ListArchiveDocumentsRequest { ArchiveId = any }),
            ["SearchDocuments"] = async () =>
                await Documents.SearchDocumentsAsync(new SearchDocumentsRequest { Query = "invoice" }),
            ["RenameDocument"] = async () =>
                await Documents.RenameDocumentAsync(new RenameDocumentRequest { DocumentId = any, Title = "x" }),
            ["DeleteDocument"] = async () =>
                await Documents.DeleteDocumentAsync(new DeleteDocumentRequest { DocumentId = any }),
            ["FileDocument"] = async () =>
                await Documents.FileDocumentAsync(new FileDocumentRequest { DocumentId = any, ArchiveId = any }),
            ["UnfileDocument"] = async () =>
                await Documents.UnfileDocumentAsync(new UnfileDocumentRequest { DocumentId = any }),
        };

        foreach (var (method, call) in calls)
        {
            var failure = await GrpcFailure.Of(call);

            await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.Unauthenticated).Because(method);
        }
    }

    // ---- what the caller owns ----

    [Test]
    public async Task AnArchive_IsOwnedByTheCaller_AndListedForThemOnly()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");

        var created = (await ArchivesOf(alice).CreateArchiveAsync(new CreateArchiveRequest { Name = "Taxes" })).Archive;
        await ArchivesOf(bob).CreateArchiveAsync(new CreateArchiveRequest { Name = "Work" });

        await Assert.That(created.OwnerId).IsEqualTo(alice.Id);
        await Assert
            .That((await ArchivesOf(alice).ListArchivesAsync(new ListArchivesRequest())).Archives.Select(a => a.Name))
            .IsEquivalentTo(["Taxes"]);
        await Assert
            .That((await ArchivesOf(bob).ListArchivesAsync(new ListArchivesRequest())).Archives.Select(a => a.Name))
            .IsEquivalentTo(["Work"]);
    }

    [Test]
    public async Task ADocument_IsOwnedByTheCaller_AndInTheirInboxOnly()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");

        var document = await TestDocuments.UploadAsync(
            new DocumentsService.DocumentsServiceClient(pipeline.ChannelFor(alice.Id)),
            "scan.pdf",
            TestDocuments.Bytes(10)
        );

        await Assert.That(document.OwnerId).IsEqualTo(alice.Id);
        await Assert.That(await InboxOfAsync(alice)).Count().IsEqualTo(1);
        await Assert.That(await InboxOfAsync(bob)).IsEmpty();
    }

    // ---- another user's resources ----

    [Test]
    public async Task AnotherUsersDocument_IsNotFound_ToRenameDeleteFileUnfileAndDownload_AndNothingChanges()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        var alicesDocuments = new DocumentsService.DocumentsServiceClient(pipeline.ChannelFor(alice.Id));
        var bobsDocuments = new DocumentsService.DocumentsServiceClient(pipeline.ChannelFor(bob.Id));
        var bobsArchive = (
            await ArchivesOf(bob).CreateArchiveAsync(new CreateArchiveRequest { Name = "Work" })
        ).Archive;
        var document = await TestDocuments.UploadAsync(
            alicesDocuments,
            "scan.pdf",
            TestDocuments.Bytes(10),
            title: "Invoice"
        );

        var renaming = await GrpcFailure.Of(async () =>
            await bobsDocuments.RenameDocumentAsync(
                new RenameDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    Title = "Mine",
                }
            )
        );
        var deleting = await GrpcFailure.Of(async () =>
            await bobsDocuments.DeleteDocumentAsync(
                new DeleteDocumentRequest { DocumentId = document.Id, Version = document.Version }
            )
        );
        var filing = await GrpcFailure.Of(async () =>
            await bobsDocuments.FileDocumentAsync(
                new FileDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    ArchiveId = bobsArchive.Id,
                }
            )
        );
        var unfiling = await GrpcFailure.Of(async () =>
            await bobsDocuments.UnfileDocumentAsync(
                new UnfileDocumentRequest { DocumentId = document.Id, Version = document.Version }
            )
        );
        var downloading = await GrpcFailure.Of(async () =>
            await TestDocuments.DownloadAsync(bobsDocuments, document.Id)
        );

        foreach (var failure in new[] { renaming, deleting, filing, unfiling, downloading })
        {
            await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.NotFound);
            await Assert.That(failure.GetRpcStatus()!.Details).IsEmpty();
        }

        var inbox = await InboxOfAsync(alice);
        await Assert.That(inbox.Single().Title).IsEqualTo("Invoice");
        await Assert.That(inbox.Single().Version).IsEqualTo(document.Version);
        await Assert
            .That(
                (
                    await bobsDocuments.ListArchiveDocumentsAsync(
                        new ListArchiveDocumentsRequest { ArchiveId = bobsArchive.Id }
                    )
                ).Documents
            )
            .IsEmpty();
    }

    [Test]
    public async Task AnotherUsersArchive_IsNotFound_ToRenameAndDelete_AndStaysAsItWas()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        var archive = (await ArchivesOf(alice).CreateArchiveAsync(new CreateArchiveRequest { Name = "Taxes" })).Archive;

        var renaming = await GrpcFailure.Of(async () =>
            await ArchivesOf(bob)
                .RenameArchiveAsync(
                    new RenameArchiveRequest
                    {
                        ArchiveId = archive.Id,
                        Version = archive.Version,
                        Name = "Mine",
                    }
                )
        );
        var deleting = await GrpcFailure.Of(async () =>
            await ArchivesOf(bob)
                .DeleteArchiveAsync(new DeleteArchiveRequest { ArchiveId = archive.Id, Version = archive.Version })
        );

        await Assert.That(renaming.StatusCode).IsEqualTo(StatusCode.NotFound);
        await Assert.That(deleting.StatusCode).IsEqualTo(StatusCode.NotFound);
        var listed = (await ArchivesOf(alice).ListArchivesAsync(new ListArchivesRequest())).Archives.Single();
        await Assert.That(listed.Name).IsEqualTo("Taxes");
        await Assert.That(listed.Version).IsEqualTo(archive.Version);
    }

    [Test]
    public async Task ListingAnotherUsersArchive_IsEmpty_NotRefused()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        var alicesDocuments = new DocumentsService.DocumentsServiceClient(pipeline.ChannelFor(alice.Id));
        var archive = (await ArchivesOf(alice).CreateArchiveAsync(new CreateArchiveRequest { Name = "Taxes" })).Archive;

        foreach (var title in new[] { "First", "Second" })
        {
            var document = await TestDocuments.UploadAsync(
                alicesDocuments,
                $"{title}.pdf",
                TestDocuments.Bytes(10),
                title: title
            );
            await alicesDocuments.FileDocumentAsync(
                new FileDocumentRequest
                {
                    DocumentId = document.Id,
                    Version = document.Version,
                    ArchiveId = archive.Id,
                }
            );
        }

        var seenByBob = await new DocumentsService.DocumentsServiceClient(
            pipeline.ChannelFor(bob.Id)
        ).ListArchiveDocumentsAsync(new ListArchiveDocumentsRequest { ArchiveId = archive.Id });
        var seenByAlice = await alicesDocuments.ListArchiveDocumentsAsync(
            new ListArchiveDocumentsRequest { ArchiveId = archive.Id }
        );

        await Assert.That(seenByBob.Documents).IsEmpty();
        await Assert.That(seenByAlice.Documents).Count().IsEqualTo(2);
    }

    private async Task<IList<DocumentDto>> InboxOfAsync(UserDto user) =>
        [
            .. (
                await new DocumentsService.DocumentsServiceClient(pipeline.ChannelFor(user.Id)).ListInboxAsync(
                    new ListInboxRequest()
                )
            ).Documents,
        ];

    public async ValueTask DisposeAsync() => await pipeline.DisposeAsync();
}
