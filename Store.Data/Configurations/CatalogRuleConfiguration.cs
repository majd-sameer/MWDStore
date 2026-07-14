using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class CatalogRuleConfiguration : IEntityTypeConfiguration<CatalogRule>
{
    public void Configure(EntityTypeBuilder<CatalogRule> builder)
    {
        builder.ToTable("CatalogRule");

        builder.ConfigureRuleColumns();

        builder.HasMany(d => d.CustomerGroups).WithMany(p => p.CatalogRules)
            .UsingEntity<Dictionary<string, object>>(
                "CatalogRuleCustomerGroup",
                r => r.HasOne<CustomerGroup>().WithMany()
                    .HasForeignKey("CustomerGroupId")
                    .OnDelete(DeleteBehavior.ClientSetNull),
                l => l.HasOne<CatalogRule>().WithMany()
                    .HasForeignKey("CatalogRuleId")
                    .OnDelete(DeleteBehavior.ClientSetNull),
                j =>
                {
                    j.HasKey("CatalogRuleId", "CustomerGroupId");
                    j.ToTable("CatalogRuleCustomerGroup");
                    j.HasIndex(new[] { "CustomerGroupId" }, "IX_Pricing_CatalogRuleCustomerGroup_CustomerGroupId");
                });
    }
}
