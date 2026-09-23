using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OfficeRkyv.Api.Data;

internal abstract class ConfigurationBase<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : class, IEntity
{
    public abstract string TableName { get; }

    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.CreatedAt).ValueGeneratedOnAdd();
        builder.Property(entity => entity.UpdatedAt).IsRowVersion();
        builder.Property(entity => entity.Status).IsRequired();
    }
}
