using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class OrderAddressConfiguration : IEntityTypeConfiguration<OrderAddress>
{
    public void Configure(EntityTypeBuilder<OrderAddress> builder)
    {
        builder.ToTable("OrderAddress");

        builder.HasIndex(e => e.CountryId, "IX_Orders_OrderAddress_CountryId");

        builder.HasIndex(e => e.DistrictId, "IX_Orders_OrderAddress_DistrictId");

        builder.HasIndex(e => e.StateOrProvinceId, "IX_Orders_OrderAddress_StateOrProvinceId");

        builder.Property(e => e.AddressLine1).HasMaxLength(450);
        builder.Property(e => e.AddressLine2).HasMaxLength(450);
        builder.Property(e => e.City).HasMaxLength(450);
        builder.Property(e => e.ContactName).HasMaxLength(450);
        builder.Property(e => e.Phone).HasMaxLength(450);
        builder.Property(e => e.ZipCode).HasMaxLength(450);

        builder.HasOne(d => d.Country).WithMany(p => p.OrderAddresses).HasForeignKey(d => d.CountryId);

        builder.HasOne(d => d.District).WithMany(p => p.OrderAddresses).HasForeignKey(d => d.DistrictId);

        builder.HasOne(d => d.StateOrProvince).WithMany(p => p.OrderAddresses)
            .HasForeignKey(d => d.StateOrProvinceId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
