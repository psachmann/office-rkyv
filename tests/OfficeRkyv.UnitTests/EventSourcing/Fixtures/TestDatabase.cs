using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OfficeRkyv.UnitTests.EventSourcing.Fixtures;

/// <summary>
/// A fresh, real SQLite database per test (kept alive via an open in-memory connection), so tests
/// exercise EF Core's actual query translation and constraints instead of an in-memory fake.
/// </summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private TestDatabase(SqliteConnection connection, EventSourcingTestContext context)
    {
        this.connection = connection;
        Context = context;
    }

    public EventSourcingTestContext Context { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<EventSourcingTestContext>().UseSqlite(connection).Options;
        var context = new EventSourcingTestContext(options);
        await context.Database.EnsureCreatedAsync();

        return new TestDatabase(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}
