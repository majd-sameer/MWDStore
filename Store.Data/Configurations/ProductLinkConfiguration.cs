using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductLinkConfiguration : IEntityTypeConfiguration<ProductLink>
{
    public void Configure(EntityTypeBuilder<ProductLink> builder)
    {
        builder.ToTable("ProductLink");

        builder.HasIndex(e => e.LinkedProductId, "IX_Catalog_ProductLink_LinkedProductId");

        builder.HasIndex(e => e.ProductId, "IX_Catalog_ProductLink_ProductId");

        builder.HasOne(d => d.LinkedProduct).WithMany(p => p.ProductLinkLinkedProducts)
            .HasForeignKey(d => d.LinkedProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Product).WithMany(p => p.ProductLinkProducts)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
