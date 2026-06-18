using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("Coupon");

        builder.HasIndex(e => e.CartRuleId, "IX_Pricing_Coupon_CartRuleId");

        builder.Property(e => e.Code).HasMaxLength(450);

        builder.HasOne(d => d.CartRule).WithMany(p => p.Coupons).HasForeignKey(d => d.CartRuleId);
    }
}
