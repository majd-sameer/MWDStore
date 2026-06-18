using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ShipmentItemConfiguration : IEntityTypeConfiguration<ShipmentItem>
{
    public void Configure(EntityTypeBuilder<ShipmentItem> builder)
    {
        builder.ToTable("ShipmentItem");

        builder.HasIndex(e => e.ProductId, "IX_Shipments_ShipmentItem_ProductId");

        builder.HasIndex(e => e.ShipmentId, "IX_Shipments_ShipmentItem_ShipmentId");

        builder.HasOne(d => d.Product).WithMany(p => p.ShipmentItems)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Shipment).WithMany(p => p.ShipmentItems)
            .HasForeignKey(d => d.ShipmentId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
