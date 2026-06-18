using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resource");

        builder.HasIndex(e => e.CultureId, "IX_Localization_Resource_CultureId");

        builder.Property(e => e.Key).HasMaxLength(450);

        builder.HasOne(d => d.Culture).WithMany(p => p.Resources).HasForeignKey(d => d.CultureId);
    }
}
