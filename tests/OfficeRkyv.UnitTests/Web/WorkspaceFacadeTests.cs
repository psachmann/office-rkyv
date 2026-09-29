extern alias web;

using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using DotNetQuery.Core;
using DotNetQuery.Extensions.DependencyInjection;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using OfficeRkyv.Contracts.Archives;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;
using web::OfficeRkyv.Web.Shared;

namespace OfficeRkyv.UnitTests.Web;

public sealed class WorkspaceFacadeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly Guid Owner = Guid.NewGuid();

    private readonly FakeArchivesClient archives = new();
    private readonly FakeDocumentsClient documents = new();
    private readonly FakeUserClient users = new();

    private ServiceProvider CreateServices() =>
        new ServiceCollection()
            .AddDotNetQuery()
            .AddSingleton<ArchivesService.ArchivesServiceClient>(archives)
            .AddSingleton<DocumentsService.DocumentsServiceClient>(documents)
            .AddSingleton<UsersService.UsersServiceClient>(users)
            .AddScoped<ArchivesFacade>()
            .AddScoped<DocumentsFacade>()
            .AddScoped<UsersFacade>()
            .BuildServiceProvider();

    private static Task<T> Next<T>(IObservable<T> stream) => stream.FirstAsync().Timeout(Timeout).ToTask();

    // A query only refetches while something is subscribed, as a rendered page is: an invalidation with no
    // subscriber is deferred until the next one joins. Tests that count fetches hold a subscription open.
    private static IDisposable Watch<TArgs, TData>(IQuery<TArgs, TData> query)
        where TData : class => query.State.Subscribe(_ => { });

    // ---- users ----

    [Test]
    public async Task FindUserById_ReportsAFoundUser_AndAnUnknownOneAsNotFound()
    {
        var known = Guid.NewGuid();
        users.Known[known] = new UserDto { Id = known.ToString(), DisplayName = "Ada" };
        using var services = CreateServices();
        var facade = services.GetRequiredService<UsersFacade>();

        facade.FindUserById.SetArgs(known);
        var found = await Next(facade.FindUserById.Success);
        facade.FindUserById.SetArgs(Guid.NewGuid());
        var unknown = await Next(facade.FindUserById.Success.Where(response => response.User is null));

        await Assert.That(found.User.DisplayName).IsEqualTo("Ada");
        await Assert.That(unknown.User).IsNull();
    }

    // ---- archives ----

    [Test]
    public async Task FindArchives_FetchesTheOwnersArchives()
    {
        archives.Items.Add(
            new ArchiveDto
            {
                Id = Guid.NewGuid().ToString(),
                OwnerId = Owner.ToString(),
                Name = "Taxes",
            }
        );
        using var services = CreateServices();
        var facade = services.GetRequiredService<ArchivesFacade>();

        facade.FindArchives.SetArgs(Owner);

        await Assert.That((await Next(facade.FindArchives.Success)).Select(a => a.Name)).IsEquivalentTo(["Taxes"]);
        await Assert.That(archives.ListedOwners).IsEquivalentTo([Owner.ToString()]);
    }

    [Test]
    public async Task ASuccessfulArchiveCreate_RefetchesTheList_SoTheNewArchiveAppears()
    {
        using var services = CreateServices();
        var facade = services.GetRequiredService<ArchivesFacade>();
        facade.FindArchives.SetArgs(Owner);
        await Assert.That(await Next(facade.FindArchives.Success)).IsEmpty();

        var refetched = Next(facade.FindArchives.Success.Where(list => list.Count == 1));
        using var createArchive = facade.CreateCreateArchiveMutation();
        createArchive.Execute(new CreateArchiveRequest { OwnerId = Owner.ToString(), Name = "Household" });

        await Assert.That((await refetched).Single().Name).IsEqualTo("Household");
    }

    [Test]
    public async Task AFailedArchiveCreate_ReportsTheApiError_AndDoesNotRefetch()
    {
        archives.MutationFailure = new RpcException(new Status(StatusCode.InvalidArgument, "bad"));
        using var services = CreateServices();
        var facade = services.GetRequiredService<ArchivesFacade>();
        using var watchArchives = Watch(facade.FindArchives);
        facade.FindArchives.SetArgs(Owner);
        await Next(facade.FindArchives.Success);
        var callsBefore = archives.ListCalls;

        using var createArchive = facade.CreateCreateArchiveMutation();
        var failed = Next(createArchive.Failure);
        createArchive.Execute(new CreateArchiveRequest { OwnerId = Owner.ToString(), Name = " " });

        await Assert.That(((RpcException)await failed).StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Task.Delay(200);
        await Assert.That(archives.ListCalls).IsEqualTo(callsBefore);
    }

    [Test]
    public async Task DeletingAnArchive_RefreshesTheInboxAgain_OnceTheCascadeHadTimeToLand()
    {
        using var services = CreateServices();
        var archiveFacade = services.GetRequiredService<ArchivesFacade>();
        var documentFacade = services.GetRequiredService<DocumentsFacade>();
        documentFacade.FindInbox.SetArgs(Owner);
        await Next(documentFacade.FindInbox.Success);

        // The list a moment after the delete is still empty; the returned document arrives with the cascade.
        using var deleteArchive = archiveFacade.CreateDeleteArchiveMutation();
        var deleted = Next(deleteArchive.Success);
        deleteArchive.Execute(new DeleteArchiveRequest { ArchiveId = Guid.NewGuid().ToString(), Version = 1 });
        await deleted;
        documents.Items.Add(
            new DocumentDto
            {
                Id = Guid.NewGuid().ToString(),
                OwnerId = Owner.ToString(),
                Title = "Returned",
            }
        );

        var later = Next(documentFacade.FindInbox.Success.Where(list => list.Count == 1));

        await Assert.That((await later).Single().Title).IsEqualTo("Returned");
    }

    // ---- documents ----

    [Test]
    public async Task ASuccessfulDocumentCreate_RefetchesTheInbox()
    {
        using var services = CreateServices();
        var facade = services.GetRequiredService<DocumentsFacade>();
        facade.FindInbox.SetArgs(Owner);
        await Assert.That(await Next(facade.FindInbox.Success)).IsEmpty();

        var refetched = Next(facade.FindInbox.Success.Where(list => list.Count == 1));
        using var createDocument = facade.CreateCreateDocumentMutation();
        createDocument.Execute(new CreateDocumentRequest { OwnerId = Owner.ToString(), Title = "Invoice" });

        await Assert.That((await refetched).Single().Title).IsEqualTo("Invoice");
    }

    [Test]
    public async Task Filing_RefreshesTheInbox_TheArchivesContents_AndTheArchiveList()
    {
        var archiveId = Guid.NewGuid();
        using var services = CreateServices();
        var documentFacade = services.GetRequiredService<DocumentsFacade>();
        var archiveFacade = services.GetRequiredService<ArchivesFacade>();
        using var watchInbox = Watch(documentFacade.FindInbox);
        using var watchContents = Watch(documentFacade.FindArchiveDocuments);
        using var watchArchives = Watch(archiveFacade.FindArchives);
        documentFacade.FindInbox.SetArgs(Owner);
        documentFacade.FindArchiveDocuments.SetArgs(archiveId);
        archiveFacade.FindArchives.SetArgs(Owner);
        await Next(documentFacade.FindInbox.Success);
        await Next(documentFacade.FindArchiveDocuments.Success);
        await Next(archiveFacade.FindArchives.Success);
        var (inbox, contents, archiveList) = (documents.InboxCalls, documents.ContentsCalls, archives.ListCalls);

        using var fileDocument = documentFacade.CreateFileDocumentMutation();
        var filed = Next(fileDocument.Success);
        fileDocument.Execute(
            new FileDocumentRequest
            {
                DocumentId = Guid.NewGuid().ToString(),
                Version = 1,
                ArchiveId = archiveId.ToString(),
            }
        );
        await filed;
        await Task.Delay(300);

        await Assert.That(documents.InboxCalls).IsGreaterThan(inbox);
        await Assert.That(documents.ContentsCalls).IsGreaterThan(contents);
        await Assert.That(archives.ListCalls).IsGreaterThan(archiveList);
    }

    [Test]
    public async Task Renaming_RefreshesTheDocumentLists_ButNotTheArchiveList()
    {
        using var services = CreateServices();
        var documentFacade = services.GetRequiredService<DocumentsFacade>();
        var archiveFacade = services.GetRequiredService<ArchivesFacade>();
        using var watchInbox = Watch(documentFacade.FindInbox);
        using var watchArchives = Watch(archiveFacade.FindArchives);
        documentFacade.FindInbox.SetArgs(Owner);
        archiveFacade.FindArchives.SetArgs(Owner);
        await Next(documentFacade.FindInbox.Success);
        await Next(archiveFacade.FindArchives.Success);
        var (inbox, archiveList) = (documents.InboxCalls, archives.ListCalls);

        using var renameDocument = documentFacade.CreateRenameDocumentMutation();
        var renamed = Next(renameDocument.Success);
        renameDocument.Execute(
            new RenameDocumentRequest
            {
                DocumentId = Guid.NewGuid().ToString(),
                Version = 1,
                Title = "New",
            }
        );
        await renamed;
        await Task.Delay(300);

        await Assert.That(documents.InboxCalls).IsGreaterThan(inbox);
        await Assert.That(archives.ListCalls).IsEqualTo(archiveList);
    }

    [Test]
    public async Task AFailedFiling_ReportsTheApiError_AndDoesNotRefetch()
    {
        documents.MutationFailure = new RpcException(new Status(StatusCode.Aborted, "changed"));
        using var services = CreateServices();
        var facade = services.GetRequiredService<DocumentsFacade>();
        using var watchInbox = Watch(facade.FindInbox);
        facade.FindInbox.SetArgs(Owner);
        await Next(facade.FindInbox.Success);
        var callsBefore = documents.InboxCalls;

        using var fileDocument = facade.CreateFileDocumentMutation();
        var failed = Next(fileDocument.Failure);
        fileDocument.Execute(
            new FileDocumentRequest
            {
                DocumentId = Guid.NewGuid().ToString(),
                Version = 1,
                ArchiveId = Guid.NewGuid().ToString(),
            }
        );

        await Assert.That(((RpcException)await failed).StatusCode).IsEqualTo(StatusCode.Aborted);
        await Task.Delay(200);
        await Assert.That(documents.InboxCalls).IsEqualTo(callsBefore);
    }

    private static AsyncUnaryCall<T> Call<T>(Task<T> response) =>
        new(response, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });

    private sealed class FakeUserClient : UsersService.UsersServiceClient
    {
        public Dictionary<Guid, UserDto> Known { get; } = [];

        public override AsyncUnaryCall<FindUserByIdResponse> FindUserByIdAsync(
            FindUserByIdRequest request,
            CallOptions options
        )
        {
            var response = new FindUserByIdResponse();

            if (Known.TryGetValue(Guid.Parse(request.UserId), out var user))
            {
                response.User = user;
            }

            return Call(Task.FromResult(response));
        }
    }

    private sealed class FakeArchivesClient : ArchivesService.ArchivesServiceClient
    {
        private int listCalls;

        public List<ArchiveDto> Items { get; } = [];
        public List<string> ListedOwners { get; } = [];
        public RpcException? MutationFailure { get; set; }
        public int ListCalls => Volatile.Read(ref listCalls);

        public override AsyncUnaryCall<ListArchivesResponse> ListArchivesAsync(
            ListArchivesRequest request,
            CallOptions options
        )
        {
            Interlocked.Increment(ref listCalls);
            lock (ListedOwners)
            {
                ListedOwners.Add(request.OwnerId);
            }

            return Call(Task.FromResult(new ListArchivesResponse { Archives = { Items } }));
        }

        public override AsyncUnaryCall<CreateArchiveResponse> CreateArchiveAsync(
            CreateArchiveRequest request,
            CallOptions options
        )
        {
            if (MutationFailure is not null)
            {
                return Call(Task.FromException<CreateArchiveResponse>(MutationFailure));
            }

            var archive = new ArchiveDto
            {
                Id = Guid.NewGuid().ToString(),
                OwnerId = request.OwnerId,
                Name = request.Name,
            };
            Items.Add(archive);

            return Call(Task.FromResult(new CreateArchiveResponse { Archive = archive }));
        }

        public override AsyncUnaryCall<Empty> DeleteArchiveAsync(DeleteArchiveRequest request, CallOptions options) =>
            Call(Task.FromResult(new Empty()));
    }

    private sealed class FakeDocumentsClient : DocumentsService.DocumentsServiceClient
    {
        private int inboxCalls;
        private int contentsCalls;

        public List<DocumentDto> Items { get; } = [];
        public RpcException? MutationFailure { get; set; }
        public int InboxCalls => Volatile.Read(ref inboxCalls);
        public int ContentsCalls => Volatile.Read(ref contentsCalls);

        public override AsyncUnaryCall<ListInboxResponse> ListInboxAsync(ListInboxRequest request, CallOptions options)
        {
            Interlocked.Increment(ref inboxCalls);

            return Call(Task.FromResult(new ListInboxResponse { Documents = { Items } }));
        }

        public override AsyncUnaryCall<ListArchiveDocumentsResponse> ListArchiveDocumentsAsync(
            ListArchiveDocumentsRequest request,
            CallOptions options
        )
        {
            Interlocked.Increment(ref contentsCalls);

            return Call(Task.FromResult(new ListArchiveDocumentsResponse()));
        }

        public override AsyncUnaryCall<CreateDocumentResponse> CreateDocumentAsync(
            CreateDocumentRequest request,
            CallOptions options
        )
        {
            var document = new DocumentDto
            {
                Id = Guid.NewGuid().ToString(),
                OwnerId = request.OwnerId,
                Title = request.Title,
            };
            Items.Add(document);

            return Call(Task.FromResult(new CreateDocumentResponse { Document = document }));
        }

        public override AsyncUnaryCall<RenameDocumentResponse> RenameDocumentAsync(
            RenameDocumentRequest request,
            CallOptions options
        ) => Call(Task.FromResult(new RenameDocumentResponse { Document = new DocumentDto { Title = request.Title } }));

        public override AsyncUnaryCall<FileDocumentResponse> FileDocumentAsync(
            FileDocumentRequest request,
            CallOptions options
        ) =>
            MutationFailure is not null
                ? Call(Task.FromException<FileDocumentResponse>(MutationFailure))
                : Call(
                    Task.FromResult(
                        new FileDocumentResponse { Document = new DocumentDto { ArchiveId = request.ArchiveId } }
                    )
                );
    }
}
