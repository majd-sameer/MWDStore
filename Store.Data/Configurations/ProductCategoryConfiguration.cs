using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategory");

        builder.HasIndex(e => e.CategoryId, "IX_Catalog_ProductCategory_CategoryId");

        builder.HasIndex(e => e.ProductId, "IX_Catalog_ProductCategory_ProductId");

        builder.HasOne(d => d.Category).WithMany(p => p.ProductCategories)
            .HasForeignKey(d => d.CategoryId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Product).WithMany(p => p.ProductCategories)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
