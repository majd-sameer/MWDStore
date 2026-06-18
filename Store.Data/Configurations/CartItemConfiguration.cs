using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItem");

        builder.HasIndex(e => e.CustomerId, "IX_ShoppingCart_CartItem_CustomerId");

        builder.HasIndex(e => e.ProductId, "IX_ShoppingCart_CartItem_ProductId");

        builder.HasOne(d => d.Customer).WithMany(p => p.CartItems)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Product).WithMany(p => p.CartItems)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
