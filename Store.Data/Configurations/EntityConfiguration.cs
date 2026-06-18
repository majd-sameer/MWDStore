using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class EntityConfiguration : IEntityTypeConfiguration<Entity>
{
    public void Configure(EntityTypeBuilder<Entity> builder)
    {
        builder.ToTable("Entity");

        builder.HasIndex(e => e.EntityTypeId, "IX_Core_Entity_EntityTypeId");

        builder.Property(e => e.Name).HasMaxLength(450);
        builder.Property(e => e.Slug).HasMaxLength(450);

        builder.HasOne(d => d.EntityType).WithMany(p => p.Entities).HasForeignKey(d => d.EntityTypeId);
    }
}
