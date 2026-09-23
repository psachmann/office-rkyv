using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OfficeRkyv.Api.Data.Configurations;

internal sealed class EventRecordConfiguration : IEntityTypeConfiguration<EventRecord>
{
    public void Configure(EntityTypeBuilder<EventRecord> builder)
    {
        builder.ToTable("events");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.EntityId, entity.Version }).IsUnique();
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.EntityId).IsRequired();
        builder.Property(entity => entity.Version).IsRequired();
        builder.Property(entity => entity.EventType).IsRequired();
        builder.Property(entity => entity.PayloadJson).IsRequired();
    }
}
