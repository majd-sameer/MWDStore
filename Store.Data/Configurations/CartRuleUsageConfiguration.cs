using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class CartRuleUsageConfiguration : IEntityTypeConfiguration<CartRuleUsage>
{
    public void Configure(EntityTypeBuilder<CartRuleUsage> builder)
    {
        builder.ToTable("CartRuleUsage");

        builder.HasIndex(e => e.CartRuleId, "IX_Pricing_CartRuleUsage_CartRuleId");

        builder.HasIndex(e => e.CouponId, "IX_Pricing_CartRuleUsage_CouponId");

        builder.HasIndex(e => e.UserId, "IX_Pricing_CartRuleUsage_UserId");

        builder.HasOne(d => d.CartRule).WithMany(p => p.CartRuleUsages)
            .HasForeignKey(d => d.CartRuleId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Coupon).WithMany(p => p.CartRuleUsages).HasForeignKey(d => d.CouponId);

        builder.HasOne(d => d.User).WithMany(p => p.CartRuleUsages)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
