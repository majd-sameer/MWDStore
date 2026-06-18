using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("Stock");

        builder.HasIndex(e => e.ProductId, "IX_Inventory_Stock_ProductId");

        builder.HasIndex(e => e.WarehouseId, "IX_Inventory_Stock_WarehouseId");

        builder.HasOne(d => d.Product).WithMany(p => p.Stocks)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Warehouse).WithMany(p => p.Stocks)
            .HasForeignKey(d => d.WarehouseId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
