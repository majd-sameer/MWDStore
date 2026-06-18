using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItem");

        builder.HasIndex(e => e.OrderId, "IX_Orders_OrderItem_OrderId");

        builder.HasIndex(e => e.ProductId, "IX_Orders_OrderItem_ProductId");

        builder.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.ProductPrice).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.TaxPercent).HasColumnType("decimal(18, 2)");

        builder.HasOne(d => d.Order).WithMany(p => p.OrderItems).HasForeignKey(d => d.OrderId);

        builder.HasOne(d => d.Product).WithMany(p => p.OrderItems)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
