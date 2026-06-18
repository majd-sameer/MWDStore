using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipment");

        builder.HasIndex(e => e.CreatedById, "IX_Shipments_Shipment_CreatedById");

        builder.HasIndex(e => e.OrderId, "IX_Shipments_Shipment_OrderId");

        builder.HasIndex(e => e.WarehouseId, "IX_Shipments_Shipment_WarehouseId");

        builder.Property(e => e.TrackingNumber).HasMaxLength(450);

        builder.HasOne(d => d.CreatedBy).WithMany(p => p.Shipments)
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Order).WithMany(p => p.Shipments)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Warehouse).WithMany(p => p.Shipments)
            .HasForeignKey(d => d.WarehouseId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
