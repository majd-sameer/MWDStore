using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductAttributeValueConfiguration : IEntityTypeConfiguration<ProductAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProductAttributeValue> builder)
    {
        builder.ToTable("ProductAttributeValue");

        builder.HasIndex(e => e.AttributeId, "IX_Catalog_ProductAttributeValue_AttributeId");

        builder.HasIndex(e => e.ProductId, "IX_Catalog_ProductAttributeValue_ProductId");

        builder.HasOne(d => d.Attribute).WithMany(p => p.ProductAttributeValues)
            .HasForeignKey(d => d.AttributeId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Product).WithMany(p => p.ProductAttributeValues)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
