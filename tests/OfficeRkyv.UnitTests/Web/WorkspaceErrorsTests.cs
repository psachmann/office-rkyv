using System.Globalization;
using System.Resources;
using System.Security.Claims;
using Google.Rpc;
using Grpc.Core;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OfficeRkyv.Contracts;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;
using OfficeRkyv.UI.Auth;
using OfficeRkyv.UI.Components.Pages.Users;
using Wolverine.Grpc.Client;
using GrpcStatus = Grpc.Core.Status;
using ProtoAny = Google.Protobuf.WellKnownTypes.Any;

namespace OfficeRkyv.UnitTests.Web;

public sealed class WorkspaceErrorsTests
{
    private static RpcException InvalidArgument(params (string Field, string Code)[] violations)
    {
        var details = new BadRequest();

        foreach (var (field, code) in violations)
        {
            details.FieldViolations.Add(
                new BadRequest.Types.FieldViolation
                {
                    Field = field,
                    Reason = code,
                    Description = "Developer text the UI must never show.",
                }
            );
        }

        return new Google.Rpc.Status
        {
            Code = (int)Code.InvalidArgument,
            Message = "The request is invalid.",
            Details = { ProtoAny.Pack(details) },
        }.ToRpcException();
    }

    private static RpcException FailedPrecondition(string reason) =>
        new Google.Rpc.Status
        {
            Code = (int)Code.FailedPrecondition,
            Message = "The resource's current state does not allow this operation.",
            Details = { ProtoAny.Pack(new ErrorInfo { Reason = reason, Domain = ErrorDomain.Value }) },
        }.ToRpcException();

    [Test]
    [Arguments("content", ViolationCodes.TooLarge, "ContentTooLarge")]
    [Arguments("content", ViolationCodes.Required, "ContentRequired")]
    [Arguments("file_name", ViolationCodes.Required, "FileNameRequired")]
    [Arguments("file_name", ViolationCodes.TooLong, "FileNameTooLong")]
    [Arguments("content", "a_rule_added_later", "FieldInvalid")]
    public async Task AFileViolation_IsShownUnderTheFile_ByItsCode(string field, string code, string key)
    {
        var errors = WorkspaceErrors.From(InvalidArgument((field, code)));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(File: key));
    }

    [Test]
    [Arguments(ViolationCodes.TooShort, "QueryTooShort")]
    [Arguments(ViolationCodes.TooLong, "QueryTooLong")]
    [Arguments("a_rule_added_later", "FieldInvalid")]
    public async Task ASearchViolation_IsShownUnderTheSearchField_ByItsCode(string code, string key)
    {
        var errors = WorkspaceErrors.From(InvalidArgument(("query", code)));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(Query: key));
    }

    [Test]
    public async Task AStaleWrite_IsTheChangedElsewhereMessage_AndAsksForAReload()
    {
        var errors = WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.Aborted, "server text")));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ChangedElsewhere", ReloadNeeded: true));
    }

    [Test]
    public async Task NotFound_IsTheGoneMessage_AndAsksForAReload()
    {
        var errors = WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.NotFound, "server text")));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "NotFound", ReloadNeeded: true));
    }

    [Test]
    public async Task Unauthenticated_IsTheSignedOutResult_NotAnEmptyOrFailedList()
    {
        var errors = WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.Unauthenticated, "server text")));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "SignedOut", SignedOut: true));
    }

    [Test]
    public async Task ARefusalThatEndsTheSignIn_SignsOut_AndAnyOtherRefusalDoesNot()
    {
        var signIn = new CountingSignInService();

        var ended = await WorkspaceErrors.EndSignInIfRefusedAsync(
            new RpcException(new GrpcStatus(StatusCode.Unauthenticated, "")),
            signIn
        );
        var wrapped = await WorkspaceErrors.EndSignInIfRefusedAsync(
            new InvalidOperationException(
                "wrapped by the typed client",
                new RpcException(new GrpcStatus(StatusCode.Unauthenticated, ""))
            ),
            signIn
        );
        var other = await WorkspaceErrors.EndSignInIfRefusedAsync(
            new RpcException(new GrpcStatus(StatusCode.NotFound, "")),
            signIn
        );

        await Assert.That(ended).IsTrue();
        await Assert.That(wrapped).IsTrue();
        await Assert.That(other).IsFalse();
        await Assert.That(signIn.SignedOut).IsEqualTo(2);
    }

    private sealed class CountingSignInService : ISignInService
    {
        public int SignedOut { get; private set; }

        public Task SignInAsync(Guid userId) => Task.CompletedTask;

        public Task SignOutAsync()
        {
            SignedOut++;

            return Task.CompletedTask;
        }
    }

    [Test]
    [Arguments(InvariantCodes.UserDeleted, "UserDeleted", false)]
    [Arguments(InvariantCodes.ArchiveDeleted, "ArchiveDeleted", true)]
    [Arguments(InvariantCodes.DocumentDeleted, "DocumentDeleted", true)]
    [Arguments(InvariantCodes.ArchiveOwnerMismatch, "ArchiveOwnerMismatch", false)]
    [Arguments("a_rule_added_later", "ActionFailed", false)]
    public async Task FailedPrecondition_IsChosenByItsReason(string reason, string key, bool reload)
    {
        var errors = WorkspaceErrors.From(FailedPrecondition(reason));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: key, ReloadNeeded: reload));
    }

    [Test]
    public async Task FailedPrecondition_WithoutDetails_IsTheGeneralMessage()
    {
        var errors = WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.FailedPrecondition, "bad")));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ActionFailed"));
    }

    [Test]
    [Arguments("name", "required", "NameRequired")]
    [Arguments("name", "too_long", "NameTooLong")]
    [Arguments("name", "something_new", "FieldInvalid")]
    public async Task InvalidArgument_MapsNameViolations(string field, string code, string expected)
    {
        var errors = WorkspaceErrors.From(InvalidArgument((field, code)));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(Name: expected));
    }

    [Test]
    [Arguments("required", "TitleRequired")]
    [Arguments("too_long", "TitleTooLong")]
    [Arguments("something_new", "FieldInvalid")]
    public async Task InvalidArgument_MapsTitleViolations(string code, string expected)
    {
        var errors = WorkspaceErrors.From(InvalidArgument(("title", code)));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(Title: expected));
    }

    [Test]
    public async Task InvalidArgument_KeepsTheFirstViolationPerField()
    {
        var errors = WorkspaceErrors.From(InvalidArgument(("title", "too_long"), ("title", "required")));

        await Assert.That(errors.Title).IsEqualTo("TitleTooLong");
    }

    [Test]
    public async Task InvalidArgument_AboutAFieldTheDialogDoesNotHave_BecomesTheGeneralMessage()
    {
        var errors = WorkspaceErrors.From(InvalidArgument(("nickname", "required")));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ActionFailed"));
    }

    [Test]
    [Arguments(StatusCode.Unavailable)]
    [Arguments(StatusCode.Internal)]
    [Arguments(StatusCode.AlreadyExists)]
    public async Task EverythingElse_IncludingAnUnreachableServer_IsTheGeneralMessage(StatusCode code)
    {
        var errors = WorkspaceErrors.From(new RpcException(new GrpcStatus(code, "SQLITE_BUSY at /secret")));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ActionFailed"));
    }

    [Test]
    public async Task AnRpcExceptionWrappedByTheTypedClient_IsUnwrapped()
    {
        var wrapped = new InvalidOperationException(
            "The state does not allow this.",
            FailedPrecondition(InvariantCodes.ArchiveDeleted)
        );

        await Assert
            .That(WorkspaceErrors.From(wrapped))
            .IsEqualTo(new WorkspaceErrors(General: "ArchiveDeleted", ReloadNeeded: true));
    }

    [Test]
    public async Task ANonRpcException_IsTheGeneralMessage()
    {
        await Assert
            .That(WorkspaceErrors.From(new HttpRequestException("connection refused")))
            .IsEqualTo(new WorkspaceErrors(General: "ActionFailed"));
    }

    [Test]
    public async Task EveryKeyItCanProduce_ExistsInTheResourceFile()
    {
        var resources = new ResourceManager(
            "OfficeRkyv.UI.Localization.Resources.Components.Pages.Users.WorkspaceErrors",
            typeof(WorkspaceErrors).Assembly
        );

        var produced = new List<WorkspaceErrors>
        {
            WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.Aborted, ""))),
            WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.NotFound, ""))),
            WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.Unauthenticated, ""))),
            WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.Unavailable, ""))),
            WorkspaceErrors.From(new InvalidOperationException()),
            WorkspaceErrors.From(InvalidArgument(("nickname", "required"))),
            WorkspaceErrors.From(new RpcException(new GrpcStatus(StatusCode.FailedPrecondition, ""))),
        };

        foreach (
            var reason in new[]
            {
                InvariantCodes.UserDeleted,
                InvariantCodes.ArchiveDeleted,
                InvariantCodes.DocumentDeleted,
                InvariantCodes.ArchiveOwnerMismatch,
                "unknown_reason",
            }
        )
        {
            produced.Add(WorkspaceErrors.From(FailedPrecondition(reason)));
        }

        foreach (var field in new[] { "name", "title" })
        {
            foreach (var code in new[] { "required", "too_long", "unknown_code" })
            {
                produced.Add(WorkspaceErrors.From(InvalidArgument((field, code))));
            }
        }

        var keys = produced
            .SelectMany(error => new[] { error.Name, error.Title, error.General })
            .OfType<string>()
            .Distinct()
            .ToList();

        await Assert.That(keys).IsNotEmpty();

        foreach (var key in keys)
        {
            await Assert.That(resources.GetString(key, CultureInfo.InvariantCulture)).IsNotNull();
        }
    }
}

/// <summary>
/// The mapper against real refusals, as they arrive through the Web's own typed client - the unwrapping of
/// each status code is Wolverine's, so it is checked rather than assumed. The clients are registered as the web
/// host registers them, with the caller token of whoever is signed in where the client was resolved.
/// </summary>
public sealed class WorkspaceErrorsOverTheWireTests : IAsyncDisposable
{
    private PipelineHost pipeline = null!;
    private ServiceProvider web = null!;
    private readonly List<AsyncServiceScope> scopes = [];

    [Before(Test)]
    public async Task SetUp()
    {
        pipeline = await PipelineHost.StartAsync();

        // Registered as the Web registers them, in a container with no Wolverine runtime - like the Web.
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<SignedInState>();
        services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<SignedInState>());
        AddClient<UsersService.UsersServiceClient>(services);
        AddClient<ArchivesService.ArchivesServiceClient>(services);
        AddClient<DocumentsService.DocumentsServiceClient>(services);
        web = services.BuildServiceProvider();
    }

    private void AddClient<TClient>(IServiceCollection services)
        where TClient : class
    {
        var http = services
            .AddWolverineGrpcClient<TClient>(options => options.Address = pipeline.Address)
            .HttpClientBuilder!;

        // The test host is plain HTTP; the web host reaches the API over TLS, where this is not needed.
        http.ConfigureChannel(options => options.UnsafeUseInsecureChannelCallCredentials = true);
        CallerTokenCredentials.AddTo(http, PipelineHost.CallerKey);
    }

    /// <summary>The clients of one window or circuit, with the user signed in there (nobody, when null).</summary>
    private sealed record Clients(
        UsersService.UsersServiceClient Users,
        ArchivesService.ArchivesServiceClient Archives,
        DocumentsService.DocumentsServiceClient Documents
    );

    private Clients As(UserDto? user)
    {
        var scope = web.CreateAsyncScope();
        scopes.Add(scope);

        if (user is not null)
        {
            scope.ServiceProvider.GetRequiredService<SignedInState>().SignIn(Guid.Parse(user.Id));
        }

        return new Clients(
            scope.ServiceProvider.GetRequiredService<UsersService.UsersServiceClient>(),
            scope.ServiceProvider.GetRequiredService<ArchivesService.ArchivesServiceClient>(),
            scope.ServiceProvider.GetRequiredService<DocumentsService.DocumentsServiceClient>()
        );
    }

    private static async Task<WorkspaceErrors> ErrorOf(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (Exception exception)
        {
            return WorkspaceErrors.From(exception);
        }

        throw new InvalidOperationException("Expected the call to be refused.");
    }

    private async Task<UserDto> CreateUser(string email = "alice@example.com") =>
        (await As(null).Users.CreateUserAsync(new CreateUserRequest { Email = email, DisplayName = email })).User;

    private Task<DocumentDto> Upload(UserDto owner, string title = "Invoice") =>
        TestDocuments.UploadAsync(As(owner).Documents, $"{title}.pdf", TestDocuments.Bytes(10), title: title);

    [Test]
    public async Task AStaleWrite_ReachesTheUiAsChangedElsewhere()
    {
        var alice = await CreateUser();
        var documents = As(alice).Documents;
        var created = await Upload(alice);
        await documents.RenameDocumentAsync(
            new RenameDocumentRequest
            {
                DocumentId = created.Id,
                Version = created.Version,
                Title = "Newer",
            }
        );

        var errors = await ErrorOf(async () =>
            await documents.RenameDocumentAsync(
                new RenameDocumentRequest
                {
                    DocumentId = created.Id,
                    Version = created.Version,
                    Title = "Late",
                }
            )
        );

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ChangedElsewhere", ReloadNeeded: true));
    }

    [Test]
    public async Task AnUnknownArchive_ReachesTheUiAsNotFound()
    {
        var alice = await CreateUser();
        var document = await Upload(alice);

        var errors = await ErrorOf(async () =>
            await As(alice)
                .Documents.FileDocumentAsync(
                    new FileDocumentRequest
                    {
                        DocumentId = document.Id,
                        Version = document.Version,
                        ArchiveId = Guid.NewGuid().ToString(),
                    }
                )
        );

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "NotFound", ReloadNeeded: true));
    }

    [Test]
    public async Task ADeletedArchive_ReachesTheUiAsArchiveDeleted()
    {
        var alice = await CreateUser();
        var archives = As(alice).Archives;
        var archive = (await archives.CreateArchiveAsync(new CreateArchiveRequest { Name = "Taxes" })).Archive;
        var document = await Upload(alice);
        await archives.DeleteArchiveAsync(
            new DeleteArchiveRequest { ArchiveId = archive.Id, Version = archive.Version }
        );

        var errors = await ErrorOf(async () =>
            await As(alice)
                .Documents.FileDocumentAsync(
                    new FileDocumentRequest
                    {
                        DocumentId = document.Id,
                        Version = document.Version,
                        ArchiveId = archive.Id,
                    }
                )
        );

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "ArchiveDeleted", ReloadNeeded: true));
    }

    [Test]
    public async Task AnotherUsersArchive_ReachesTheUiAsNotFound()
    {
        var alice = await CreateUser("alice@example.com");
        var bob = await CreateUser("bob@example.com");
        var bobsArchive = (
            await As(bob).Archives.CreateArchiveAsync(new CreateArchiveRequest { Name = "Work" })
        ).Archive;
        var document = await Upload(alice);

        var errors = await ErrorOf(async () =>
            await As(alice)
                .Documents.FileDocumentAsync(
                    new FileDocumentRequest
                    {
                        DocumentId = document.Id,
                        Version = document.Version,
                        ArchiveId = bobsArchive.Id,
                    }
                )
        );

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "NotFound", ReloadNeeded: true));
    }

    [Test]
    public async Task ADeletedUser_ReachesTheUiAsSignedOut_AndEndsTheSignIn()
    {
        var alice = await CreateUser();
        var signedInAsAlice = As(alice);
        await signedInAsAlice.Users.DeleteUserAsync(
            new DeleteUserRequest { UserId = alice.Id, Version = alice.Version }
        );
        var signIn = new RecordingSignInService();

        Exception? refusal = null;
        try
        {
            await Upload(alice);
        }
        catch (Exception exception)
        {
            refusal = exception;
        }

        await Assert.That(refusal).IsNotNull();
        await Assert
            .That(WorkspaceErrors.From(refusal!))
            .IsEqualTo(new WorkspaceErrors(General: "SignedOut", SignedOut: true));
        await Assert.That(await WorkspaceErrors.EndSignInIfRefusedAsync(refusal!, signIn)).IsTrue();
        await Assert.That(signIn.SignedOut).IsEqualTo(1);
    }

    [Test]
    public async Task ASignedOutVisitor_ReachesTheUiAsSignedOut()
    {
        var errors = await ErrorOf(async () => await As(null).Archives.ListArchivesAsync(new ListArchivesRequest()));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(General: "SignedOut", SignedOut: true));
    }

    [Test]
    public async Task AnEmptyFile_ReachesTheUiAsAFileMessage()
    {
        var alice = await CreateUser();

        var errors = await ErrorOf(async () => await TestDocuments.UploadAsync(As(alice).Documents, "empty.txt", []));

        await Assert.That(errors).IsEqualTo(new WorkspaceErrors(File: "ContentRequired"));
    }

    [Test]
    public async Task InvalidInput_ReachesTheUiAsFieldMessages()
    {
        var alice = await CreateUser();

        var title = await ErrorOf(async () =>
            await TestDocuments.UploadAsync(As(alice).Documents, "a.pdf", TestDocuments.Bytes(3), title: " ")
        );
        var name = await ErrorOf(async () =>
            await As(alice).Archives.CreateArchiveAsync(new CreateArchiveRequest { Name = new string('n', 101) })
        );

        await Assert.That(title).IsEqualTo(new WorkspaceErrors(Title: "TitleRequired"));
        await Assert.That(name).IsEqualTo(new WorkspaceErrors(Name: "NameTooLong"));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in scopes)
        {
            await scope.DisposeAsync();
        }

        await web.DisposeAsync();
        await pipeline.DisposeAsync();
    }

    /// <summary>Who is signed in where a client was resolved: the window or circuit's authentication state.</summary>
    private sealed class SignedInState : AuthenticationStateProvider
    {
        private ClaimsPrincipal user = SignedInUser.Anonymous;

        public void SignIn(Guid userId) => user = SignedInUser.PrincipalFor(userId);

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }

    private sealed class RecordingSignInService : ISignInService
    {
        public int SignedOut { get; private set; }

        public Task SignInAsync(Guid userId) => Task.CompletedTask;

        public Task SignOutAsync()
        {
            SignedOut++;

            return Task.CompletedTask;
        }
    }
}
