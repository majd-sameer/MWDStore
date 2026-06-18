using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class PriceAndDestinationConfiguration : IEntityTypeConfiguration<PriceAndDestination>
{
    public void Configure(EntityTypeBuilder<PriceAndDestination> builder)
    {
        builder.ToTable("PriceAndDestination");

        builder.HasIndex(e => e.CountryId, "IX_ShippingTableRate_PriceAndDestination_CountryId");

        builder.HasIndex(e => e.DistrictId, "IX_ShippingTableRate_PriceAndDestination_DistrictId");

        builder.HasIndex(e => e.StateOrProvinceId, "IX_ShippingTableRate_PriceAndDestination_StateOrProvinceId");

        builder.Property(e => e.MinOrderSubtotal).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.ShippingPrice).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.ZipCode).HasMaxLength(450);
        builder.Property(e => e.ShippingProviderId).HasMaxLength(450);

        builder.HasIndex(e => e.ShippingProviderId);

        builder.HasOne(d => d.Country).WithMany(p => p.PriceAndDestinations).HasForeignKey(d => d.CountryId);

        builder.HasOne(d => d.ShippingProvider).WithMany().HasForeignKey(d => d.ShippingProviderId);

        builder.HasOne(d => d.District).WithMany(p => p.PriceAndDestinations).HasForeignKey(d => d.DistrictId);

        builder.HasOne(d => d.StateOrProvince).WithMany(p => p.PriceAndDestinations).HasForeignKey(d => d.StateOrProvinceId);
    }
}
