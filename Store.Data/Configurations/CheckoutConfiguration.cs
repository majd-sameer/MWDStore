using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class CheckoutConfiguration : IEntityTypeConfiguration<Checkout>
{
    public void Configure(EntityTypeBuilder<Checkout> builder)
    {
        builder.ToTable("Checkout");

        builder.HasIndex(e => e.CreatedById, "IX_Checkouts_Checkout_CreatedById");

        builder.HasIndex(e => e.CustomerId, "IX_Checkouts_Checkout_CustomerId");

        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.CouponCode).HasMaxLength(450);
        builder.Property(e => e.CouponRuleName).HasMaxLength(450);
        builder.Property(e => e.OrderNote).HasMaxLength(1000);
        builder.Property(e => e.ShippingAmount).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.ShippingMethod).HasMaxLength(450);
        builder.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");

        builder.HasOne(d => d.CreatedBy).WithMany(p => p.CheckoutCreatedBies)
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Customer).WithMany(p => p.CheckoutCustomers)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
