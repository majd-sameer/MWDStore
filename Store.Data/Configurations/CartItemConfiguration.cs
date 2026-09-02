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

        // One line per product per shopper. Adding a product the bag already holds raises that line
        // (CartService.AddToCartAsync), and this makes the rule the database's rather than the
        // application's — two concurrent adds can otherwise both miss the existing row and insert.
        builder.HasIndex(e => new { e.CustomerId, e.ProductId }, "UX_ShoppingCart_CartItem_Customer_Product")
            .IsUnique();

        builder.HasOne(d => d.Customer).WithMany(p => p.CartItems)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Product).WithMany(p => p.CartItems)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
