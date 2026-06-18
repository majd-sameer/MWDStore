using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Order");

        builder.HasIndex(e => e.BillingAddressId, "IX_Orders_Order_BillingAddressId");

        builder.HasIndex(e => e.CreatedById, "IX_Orders_Order_CreatedById");

        builder.HasIndex(e => e.CustomerId, "IX_Orders_Order_CustomerId");

        builder.HasIndex(e => e.LatestUpdatedById, "IX_Orders_Order_LatestUpdatedById");

        builder.HasIndex(e => e.ParentId, "IX_Orders_Order_ParentId");

        builder.HasIndex(e => e.ShippingAddressId, "IX_Orders_Order_ShippingAddressId");

        // Public tracking code — unique among the orders that have one (filtered so the many
        // null sub-orders / legacy rows don't collide).
        builder.HasIndex(e => e.TrackingNumber, "IX_Orders_Order_TrackingNumber")
            .IsUnique()
            .HasFilter("[TrackingNumber] IS NOT NULL");

        builder.Property(e => e.TrackingNumber).HasMaxLength(6);
        builder.Property(e => e.GuestEmail).HasMaxLength(256);
        builder.Property(e => e.CouponCode).HasMaxLength(450);
        builder.Property(e => e.CouponRuleName).HasMaxLength(450);
        builder.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.OrderNote).HasMaxLength(1000);
        builder.Property(e => e.OrderTotal).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.PaymentFeeAmount).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.PaymentMethod).HasMaxLength(450);
        builder.Property(e => e.ShippingFeeAmount).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.ShippingMethod).HasMaxLength(450);
        builder.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.SubTotalWithDiscount).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");

        builder.HasOne(d => d.BillingAddress).WithMany(p => p.OrderBillingAddresses)
            .HasForeignKey(d => d.BillingAddressId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.CreatedBy).WithMany(p => p.OrderCreatedBies)
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Customer).WithMany(p => p.OrderCustomers)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.LatestUpdatedBy).WithMany(p => p.OrderLatestUpdatedBies)
            .HasForeignKey(d => d.LatestUpdatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Parent).WithMany(p => p.InverseParent).HasForeignKey(d => d.ParentId);

        builder.HasOne(d => d.ShippingAddress).WithMany(p => p.OrderShippingAddresses)
            .HasForeignKey(d => d.ShippingAddressId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
