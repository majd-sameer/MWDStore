using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouse");

        builder.HasIndex(e => e.AddressId, "IX_Inventory_Warehouse_AddressId");

        builder.HasIndex(e => e.VendorId, "IX_Inventory_Warehouse_VendorId");

        builder.Property(e => e.Name).HasMaxLength(450);

        builder.HasOne(d => d.Address).WithMany(p => p.Warehouses)
            .HasForeignKey(d => d.AddressId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Vendor).WithMany(p => p.Warehouses).HasForeignKey(d => d.VendorId);
    }
}
