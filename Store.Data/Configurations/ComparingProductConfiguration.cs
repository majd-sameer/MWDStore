using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ComparingProductConfiguration : IEntityTypeConfiguration<ComparingProduct>
{
    public void Configure(EntityTypeBuilder<ComparingProduct> builder)
    {
        builder.ToTable("ComparingProduct");

        builder.HasIndex(e => e.ProductId, "IX_ProductComparison_ComparingProduct_ProductId");

        builder.HasIndex(e => e.UserId, "IX_ProductComparison_ComparingProduct_UserId");

        builder.HasOne(d => d.Product).WithMany(p => p.ComparingProducts)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.User).WithMany(p => p.ComparingProducts)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
