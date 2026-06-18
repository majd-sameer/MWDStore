using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payment");

        builder.HasIndex(e => e.OrderId, "IX_Payments_Payment_OrderId");

        builder.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.GatewayTransactionId).HasMaxLength(450);
        builder.Property(e => e.PaymentFee).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.PaymentMethod).HasMaxLength(450);

        builder.HasOne(d => d.Order).WithMany(p => p.Payments)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
