using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class CartRuleConfiguration : IEntityTypeConfiguration<CartRule>
{
    public void Configure(EntityTypeBuilder<CartRule> builder)
    {
        builder.ToTable("CartRule");

        builder.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.MaxDiscountAmount).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.Name).HasMaxLength(450);
        builder.Property(e => e.RuleToApply).HasMaxLength(450);

        builder.HasMany(d => d.Categories).WithMany(p => p.CartRules)
            .UsingEntity<Dictionary<string, object>>(
                "CartRuleCategory",
                r => r.HasOne<Category>().WithMany()
                    .HasForeignKey("CategoryId")
                    .OnDelete(DeleteBehavior.ClientSetNull),
                l => l.HasOne<CartRule>().WithMany().HasForeignKey("CartRuleId"),
                j =>
                {
                    j.HasKey("CartRuleId", "CategoryId");
                    j.ToTable("CartRuleCategory");
                    j.HasIndex(new[] { "CategoryId" }, "IX_Pricing_CartRuleCategory_CategoryId");
                });

        builder.HasMany(d => d.CustomerGroups).WithMany(p => p.CartRules)
            .UsingEntity<Dictionary<string, object>>(
                "CartRuleCustomerGroup",
                r => r.HasOne<CustomerGroup>().WithMany()
                    .HasForeignKey("CustomerGroupId")
                    .OnDelete(DeleteBehavior.ClientSetNull),
                l => l.HasOne<CartRule>().WithMany().HasForeignKey("CartRuleId"),
                j =>
                {
                    j.HasKey("CartRuleId", "CustomerGroupId");
                    j.ToTable("CartRuleCustomerGroup");
                    j.HasIndex(new[] { "CustomerGroupId" }, "IX_Pricing_CartRuleCustomerGroup_CustomerGroupId");
                });

        builder.HasMany(d => d.Products).WithMany(p => p.CartRules)
            .UsingEntity<Dictionary<string, object>>(
                "CartRuleProduct",
                r => r.HasOne<Product>().WithMany()
                    .HasForeignKey("ProductId")
                    .OnDelete(DeleteBehavior.ClientSetNull),
                l => l.HasOne<CartRule>().WithMany().HasForeignKey("CartRuleId"),
                j =>
                {
                    j.HasKey("CartRuleId", "ProductId");
                    j.ToTable("CartRuleProduct");
                    j.HasIndex(new[] { "ProductId" }, "IX_Pricing_CartRuleProduct_ProductId");
                });
    }
}
