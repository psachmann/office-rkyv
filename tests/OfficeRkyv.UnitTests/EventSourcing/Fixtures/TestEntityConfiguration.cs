using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OfficeRkyv.UnitTests.EventSourcing.Fixtures;

internal sealed class TestEntityConfiguration : IEntityTypeConfiguration<TestEntity>
{
    public void Configure(EntityTypeBuilder<TestEntity> builder)
    {
        builder.ToTable("test_entities");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Version).IsRequired();
        builder.Property(entity => entity.Status).IsRequired();
        builder.Property(entity => entity.Name).IsRequired();
    }
}
