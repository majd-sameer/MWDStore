using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductTemplateConfiguration : IEntityTypeConfiguration<ProductTemplate>
{
    public void Configure(EntityTypeBuilder<ProductTemplate> builder)
    {
        builder.ToTable("ProductTemplate");

        builder.Property(e => e.Name).HasMaxLength(450);

        builder.HasMany(d => d.ProductAttributes).WithMany(p => p.ProductTemplates)
            .UsingEntity<Dictionary<string, object>>(
                "ProductTemplateProductAttribute",
                r => r.HasOne<ProductAttribute>().WithMany().HasForeignKey("ProductAttributeId"),
                l => l.HasOne<ProductTemplate>().WithMany().HasForeignKey("ProductTemplateId"),
                j =>
                {
                    j.HasKey("ProductTemplateId", "ProductAttributeId");
                    j.ToTable("ProductTemplateProductAttribute");
                    j.HasIndex(new[] { "ProductAttributeId" }, "IX_Catalog_ProductTemplateProductAttribute_ProductAttributeId");
                });
    }
}
