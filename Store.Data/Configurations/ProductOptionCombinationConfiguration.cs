using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductOptionCombinationConfiguration : IEntityTypeConfiguration<ProductOptionCombination>
{
    public void Configure(EntityTypeBuilder<ProductOptionCombination> builder)
    {
        builder.ToTable("ProductOptionCombination");

        builder.HasIndex(e => e.OptionId, "IX_Catalog_ProductOptionCombination_OptionId");

        builder.HasIndex(e => e.ProductId, "IX_Catalog_ProductOptionCombination_ProductId");

        builder.Property(e => e.Value).HasMaxLength(450);

        builder.HasOne(d => d.Option).WithMany(p => p.ProductOptionCombinations)
            .HasForeignKey(d => d.OptionId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Product).WithMany(p => p.ProductOptionCombinations)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
