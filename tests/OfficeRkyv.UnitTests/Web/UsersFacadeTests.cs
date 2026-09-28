extern alias web;

using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using DotNetQuery.Extensions.DependencyInjection;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using OfficeRkyv.Contracts.Users;
using web::OfficeRkyv.Web.Shared;

namespace OfficeRkyv.UnitTests.Web;

public sealed class UsersFacadeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly FakeUserClient client = new();

    private ServiceProvider CreateServices() =>
        new ServiceCollection()
            .AddDotNetQuery()
            .AddSingleton<UsersService.UsersServiceClient>(client)
            .AddScoped<UsersFacade>()
            .BuildServiceProvider();

    [Test]
    public async Task FindAllUsers_FetchesTheUsersFromTheApi()
    {
        client.Users.Add(new UserDto { Email = "ada@example.com", DisplayName = "Ada" });
        using var services = CreateServices();
        var users = services.GetRequiredService<UsersFacade>();
        users.FindAllUsers.SetArgs(Unit.Default);

        var fetched = await users.FindAllUsers.Success.FirstAsync().Timeout(Timeout).ToTask();

        await Assert.That(fetched.Select(user => user.Email)).IsEquivalentTo(["ada@example.com"]);
    }

    [Test]
    public async Task ASuccessfulCreate_RefetchesTheList_SoTheNewUserAppears()
    {
        using var services = CreateServices();
        var users = services.GetRequiredService<UsersFacade>();
        users.FindAllUsers.SetArgs(Unit.Default);
        await Assert.That(await users.FindAllUsers.Success.FirstAsync().Timeout(Timeout).ToTask()).IsEmpty();

        var created = users.CreateUser.Success.FirstAsync().Timeout(Timeout).ToTask();
        var refetched = users
            .FindAllUsers.Success.Where(list => list.Count == 1)
            .FirstAsync()
            .Timeout(Timeout)
            .ToTask();

        users.CreateUser.Execute(new CreateUserRequest { Email = "grace@example.com", DisplayName = "Grace" });

        await Assert.That((await created).Email).IsEqualTo("grace@example.com");
        await Assert.That((await refetched).Single().DisplayName).IsEqualTo("Grace");
    }

    [Test]
    public async Task AFailedCreate_ReportsTheApiError_AndDoesNotRefetch()
    {
        client.CreateFailure = new RpcException(new Status(StatusCode.AlreadyExists, "taken"));
        using var services = CreateServices();
        var users = services.GetRequiredService<UsersFacade>();
        users.FindAllUsers.SetArgs(Unit.Default);
        await users.FindAllUsers.Success.FirstAsync().Timeout(Timeout).ToTask();
        var listCallsBefore = client.ListCalls;

        var failed = users.CreateUser.Failure.FirstAsync().Timeout(Timeout).ToTask();
        users.CreateUser.Execute(new CreateUserRequest { Email = "dup@example.com", DisplayName = "Dup" });

        var failure = await failed;
        await Task.Delay(200);

        await Assert.That(((RpcException)failure).StatusCode).IsEqualTo(StatusCode.AlreadyExists);
        await Assert.That(client.ListCalls).IsEqualTo(listCallsBefore);
    }

    [Test]
    public async Task TheMutationState_ReplaysTheLastOutcome_WhichIsWhyTheDialogSkipsIt()
    {
        client.CreateFailure = new RpcException(new Status(StatusCode.AlreadyExists, "taken"));
        using var services = CreateServices();
        var users = services.GetRequiredService<UsersFacade>();

        var failed = users.CreateUser.Failure.FirstAsync().Timeout(Timeout).ToTask();
        users.CreateUser.Execute(new CreateUserRequest { Email = "dup@example.com", DisplayName = "Dup" });
        await failed;

        // A later subscriber - e.g. the dialog opened again - sees the old failure straight away...
        var replayed = await users.CreateUser.State.FirstAsync().Timeout(Timeout).ToTask();
        await Assert.That(replayed.IsFailure).IsTrue();

        // ...and nothing more once that replay is skipped, until it starts an execution itself.
        var afterSkip = users.CreateUser.State.Skip(1).FirstAsync().Timeout(TimeSpan.FromMilliseconds(300)).ToTask();
        await Assert.That(async () => await afterSkip).Throws<TimeoutException>();
    }

    private sealed class FakeUserClient : UsersService.UsersServiceClient
    {
        private int listCalls;

        public List<UserDto> Users { get; } = [];
        public RpcException? CreateFailure { get; set; }
        public int ListCalls => Volatile.Read(ref listCalls);

        public override AsyncUnaryCall<ListUsersResponse> ListUsersAsync(ListUsersRequest request, CallOptions options)
        {
            Interlocked.Increment(ref listCalls);

            return Call(Task.FromResult(new ListUsersResponse { Users = { Users } }));
        }

        public override AsyncUnaryCall<CreateUserResponse> CreateUserAsync(
            CreateUserRequest request,
            CallOptions options
        )
        {
            if (CreateFailure is not null)
            {
                return Call(Task.FromException<CreateUserResponse>(CreateFailure));
            }

            var user = new UserDto { Email = request.Email, DisplayName = request.DisplayName };
            Users.Add(user);

            return Call(Task.FromResult(new CreateUserResponse { User = user }));
        }

        private static AsyncUnaryCall<T> Call<T>(Task<T> response) =>
            new(response, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });
    }
}
