using Microsoft.EntityFrameworkCore;

namespace OfficeRkyv.Api.Data;

internal sealed class OfficeRkyvContext(DbContextOptions<OfficeRkyvContext> options) : DbContext(options)
{
    public DbSet<EventEntity>? Events { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(OfficeRkyvApiLib.Assembly);
    }
}
