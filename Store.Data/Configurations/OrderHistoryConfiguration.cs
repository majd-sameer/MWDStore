using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class OrderHistoryConfiguration : IEntityTypeConfiguration<OrderHistory>
{
    public void Configure(EntityTypeBuilder<OrderHistory> builder)
    {
        builder.ToTable("OrderHistory");

        builder.HasIndex(e => e.CreatedById, "IX_Orders_OrderHistory_CreatedById");

        builder.HasIndex(e => e.OrderId, "IX_Orders_OrderHistory_OrderId");

        builder.Property(e => e.Note).HasMaxLength(1000);

        builder.HasOne(d => d.CreatedBy).WithMany(p => p.OrderHistories)
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Order).WithMany(p => p.OrderHistories)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
