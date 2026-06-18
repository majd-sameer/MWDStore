using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ShippingProviderConfiguration : IEntityTypeConfiguration<ShippingProvider>
{
    public void Configure(EntityTypeBuilder<ShippingProvider> builder)
    {
        builder.ToTable("ShippingProvider");

        builder.Property(e => e.ConfigureUrl).HasMaxLength(450);
        builder.Property(e => e.Name).HasMaxLength(450);
        builder.Property(e => e.OnlyCountryIdsString).HasMaxLength(1000);
        builder.Property(e => e.OnlyStateOrProvinceIdsString).HasMaxLength(1000);
        builder.Property(e => e.ShippingPriceServiceTypeName).HasMaxLength(450);
    }
}
