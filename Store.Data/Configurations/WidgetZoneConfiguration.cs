using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class WidgetZoneConfiguration : IEntityTypeConfiguration<WidgetZone>
{
    public void Configure(EntityTypeBuilder<WidgetZone> builder)
    {
        builder.ToTable("WidgetZone");

        builder.Property(e => e.Name).HasMaxLength(450);
    }
}
