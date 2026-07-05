using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refund");

        builder.HasIndex(e => e.PaymentId, "IX_Payments_Refund_PaymentId");
        builder.HasIndex(e => e.OrderId, "IX_Orders_Refund_OrderId");

        // Idempotency guard: at most one refund per (payment, key). Filtered so the many keyless
        // refunds don't collide on NULL.
        builder.HasIndex(e => new { e.PaymentId, e.IdempotencyKey }, "IX_Payments_Refund_PaymentId_IdempotencyKey")
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.Reason).HasMaxLength(1000);
        builder.Property(e => e.ProviderRefundId).HasMaxLength(450);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(450);

        builder.HasOne(d => d.Payment).WithMany(p => p.Refunds)
            .HasForeignKey(d => d.PaymentId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Order).WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
