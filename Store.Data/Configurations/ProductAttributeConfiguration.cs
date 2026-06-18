using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> builder)
    {
        builder.ToTable("ProductAttribute");

        builder.HasIndex(e => e.GroupId, "IX_Catalog_ProductAttribute_GroupId");

        builder.Property(e => e.Name).HasMaxLength(450);

        builder.HasOne(d => d.Group).WithMany(p => p.ProductAttributes)
            .HasForeignKey(d => d.GroupId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
