using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("District");

        builder.HasIndex(e => e.StateOrProvinceId, "IX_Core_District_StateOrProvinceId");

        builder.Property(e => e.Name).HasMaxLength(450);
        builder.Property(e => e.Type).HasMaxLength(450);

        builder.HasOne(d => d.StateOrProvince).WithMany(p => p.Districts)
            .HasForeignKey(d => d.StateOrProvinceId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
