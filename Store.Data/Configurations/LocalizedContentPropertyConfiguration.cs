using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class LocalizedContentPropertyConfiguration : IEntityTypeConfiguration<LocalizedContentProperty>
{
    public void Configure(EntityTypeBuilder<LocalizedContentProperty> builder)
    {
        builder.ToTable("LocalizedContentProperty");

        builder.HasIndex(e => e.CultureId, "IX_Localization_LocalizedContentProperty_CultureId");

        builder.Property(e => e.EntityType).HasMaxLength(450);
        builder.Property(e => e.EntityKey).HasMaxLength(450);
        builder.Property(e => e.ProperyName).HasMaxLength(450);

        builder.HasIndex(e => new { e.EntityType, e.EntityKey }, "IX_Localization_LocalizedContentProperty_EntityKey");

        builder.HasOne(d => d.Culture).WithMany(p => p.LocalizedContentProperties).HasForeignKey(d => d.CultureId);
    }
}
