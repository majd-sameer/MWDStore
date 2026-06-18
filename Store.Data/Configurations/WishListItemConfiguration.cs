using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class WishListItemConfiguration : IEntityTypeConfiguration<WishListItem>
{
    public void Configure(EntityTypeBuilder<WishListItem> builder)
    {
        builder.ToTable("WishListItem");

        builder.HasIndex(e => e.ProductId, "IX_WishList_WishListItem_ProductId");

        builder.HasIndex(e => e.WishListId, "IX_WishList_WishListItem_WishListId");

        builder.HasOne(d => d.Product).WithMany(p => p.WishListItems)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.WishList).WithMany(p => p.WishListItems)
            .HasForeignKey(d => d.WishListId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
