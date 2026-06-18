using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductOptionValueConfiguration : IEntityTypeConfiguration<ProductOptionValue>
{
    public void Configure(EntityTypeBuilder<ProductOptionValue> builder)
    {
        builder.ToTable("ProductOptionValue");

        builder.HasIndex(e => e.OptionId, "IX_Catalog_ProductOptionValue_OptionId");

        builder.HasIndex(e => e.ProductId, "IX_Catalog_ProductOptionValue_ProductId");

        builder.Property(e => e.DisplayType).HasMaxLength(450);
        builder.Property(e => e.Value).HasMaxLength(450);

        builder.HasOne(d => d.Option).WithMany(p => p.ProductOptionValues)
            .HasForeignKey(d => d.OptionId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Product).WithMany(p => p.ProductOptionValues)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
