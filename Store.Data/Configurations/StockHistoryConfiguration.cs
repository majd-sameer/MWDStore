using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class StockHistoryConfiguration : IEntityTypeConfiguration<StockHistory>
{
    public void Configure(EntityTypeBuilder<StockHistory> builder)
    {
        builder.ToTable("StockHistory");

        builder.HasIndex(e => e.CreatedById, "IX_Inventory_StockHistory_CreatedById");

        builder.HasIndex(e => e.ProductId, "IX_Inventory_StockHistory_ProductId");

        builder.HasIndex(e => e.WarehouseId, "IX_Inventory_StockHistory_WarehouseId");

        builder.Property(e => e.Note).HasMaxLength(1000);

        builder.HasOne(d => d.CreatedBy).WithMany(p => p.StockHistories)
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Product).WithMany(p => p.StockHistories)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Warehouse).WithMany(p => p.StockHistories)
            .HasForeignKey(d => d.WarehouseId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
