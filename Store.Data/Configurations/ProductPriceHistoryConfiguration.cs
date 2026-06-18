using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductPriceHistoryConfiguration : IEntityTypeConfiguration<ProductPriceHistory>
{
    public void Configure(EntityTypeBuilder<ProductPriceHistory> builder)
    {
        builder.ToTable("ProductPriceHistory");

        builder.HasIndex(e => e.CreatedById, "IX_Catalog_ProductPriceHistory_CreatedById");

        builder.HasIndex(e => e.ProductId, "IX_Catalog_ProductPriceHistory_ProductId");

        builder.Property(e => e.OldPrice).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.Price).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.SpecialPrice).HasColumnType("decimal(18, 2)");

        builder.HasOne(d => d.CreatedBy).WithMany(p => p.ProductPriceHistories)
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Product).WithMany(p => p.ProductPriceHistories).HasForeignKey(d => d.ProductId);
    }
}
