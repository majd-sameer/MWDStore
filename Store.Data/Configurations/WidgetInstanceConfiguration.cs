using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class WidgetInstanceConfiguration : IEntityTypeConfiguration<WidgetInstance>
{
    public void Configure(EntityTypeBuilder<WidgetInstance> builder)
    {
        builder.ToTable("WidgetInstance");

        builder.HasIndex(e => e.WidgetId, "IX_Core_WidgetInstance_WidgetId");

        builder.HasIndex(e => e.WidgetZoneId, "IX_Core_WidgetInstance_WidgetZoneId");

        builder.Property(e => e.Name).HasMaxLength(450);

        builder.HasOne(d => d.Widget).WithMany(p => p.WidgetInstances).HasForeignKey(d => d.WidgetId);

        builder.HasOne(d => d.WidgetZone).WithMany(p => p.WidgetInstances)
            .HasForeignKey(d => d.WidgetZoneId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
