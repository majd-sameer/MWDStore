using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductMediumConfiguration : IEntityTypeConfiguration<ProductMedium>
{
    public void Configure(EntityTypeBuilder<ProductMedium> builder)
    {
        builder.HasIndex(e => e.MediaId, "IX_Catalog_ProductMedia_MediaId");

        builder.HasIndex(e => e.ProductId, "IX_Catalog_ProductMedia_ProductId");

        builder.HasOne(d => d.Media).WithMany(p => p.ProductMedia)
            .HasForeignKey(d => d.MediaId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Product).WithMany(p => p.ProductMedia)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
