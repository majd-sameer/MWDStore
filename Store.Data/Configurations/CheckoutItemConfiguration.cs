using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class CheckoutItemConfiguration : IEntityTypeConfiguration<CheckoutItem>
{
    public void Configure(EntityTypeBuilder<CheckoutItem> builder)
    {
        builder.ToTable("CheckoutItem");

        builder.HasIndex(e => e.CheckoutId, "IX_Checkouts_CheckoutItem_CheckoutId");

        builder.HasIndex(e => e.ProductId, "IX_Checkouts_CheckoutItem_ProductId");

        builder.HasOne(d => d.Checkout).WithMany(p => p.CheckoutItems)
            .HasForeignKey(d => d.CheckoutId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Product).WithMany(p => p.CheckoutItems)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
