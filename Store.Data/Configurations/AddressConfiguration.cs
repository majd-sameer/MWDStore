using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Address");

        builder.HasIndex(e => e.CountryId, "IX_Core_Address_CountryId");

        builder.HasIndex(e => e.DistrictId, "IX_Core_Address_DistrictId");

        builder.HasIndex(e => e.StateOrProvinceId, "IX_Core_Address_StateOrProvinceId");

        builder.ConfigureAddressColumns();

        builder.HasOne(d => d.Country).WithMany(p => p.Addresses)
            .HasForeignKey(d => d.CountryId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.District).WithMany(p => p.Addresses).HasForeignKey(d => d.DistrictId);

        builder.HasOne(d => d.StateOrProvince).WithMany(p => p.Addresses)
            .HasForeignKey(d => d.StateOrProvinceId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
