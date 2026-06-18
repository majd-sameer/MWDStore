using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class EntityTypeConfiguration : IEntityTypeConfiguration<EntityType>
{
    public void Configure(EntityTypeBuilder<EntityType> builder)
    {
        builder.ToTable("EntityType");

        builder.Property(e => e.AreaName).HasMaxLength(450);
        builder.Property(e => e.RoutingAction).HasMaxLength(450);
        builder.Property(e => e.RoutingController).HasMaxLength(450);
    }
}
