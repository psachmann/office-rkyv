using Microsoft.EntityFrameworkCore;
using OfficeRkyv.Api.Data.Configurations;

namespace OfficeRkyv.UnitTests.EventSourcing.Fixtures;

/// <summary>
/// Maps both the production <c>events</c> table (reusing the real <c>EventRecordConfiguration</c>)
/// and a test-only materialized entity table, so tests exercise <c>EventStore</c> and
/// <c>MaterializedEntityStore</c> against a real EF Core/SQLite model without adding a test-only
/// entity to the production <c>OfficeRkyvContext</c>.
/// </summary>
internal sealed class EventSourcingTestContext(DbContextOptions<EventSourcingTestContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EventRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TestEntityConfiguration());
    }
}
