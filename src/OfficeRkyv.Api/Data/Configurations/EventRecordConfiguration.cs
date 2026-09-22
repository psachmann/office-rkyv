using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OfficeRkyv.Api.Data.Configurations;

internal sealed class EventRecordConfiguration : IEntityTypeConfiguration<EventRecord>
{
    public void Configure(EntityTypeBuilder<EventRecord> builder)
    {
        builder.ToTable("events");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => entity.EntityId);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.EntityId).IsRequired();
        builder.Property(entity => entity.EventType).IsRequired();
        builder.Property(entity => entity.PayloadJson).IsRequired();
    }
}
