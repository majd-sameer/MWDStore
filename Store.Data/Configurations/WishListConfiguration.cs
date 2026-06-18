using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class WishListConfiguration : IEntityTypeConfiguration<WishList>
{
    public void Configure(EntityTypeBuilder<WishList> builder)
    {
        builder.ToTable("WishList");

        builder.HasIndex(e => e.UserId, "IX_WishList_WishList_UserId");

        builder.Property(e => e.SharingCode).HasMaxLength(450);

        builder.HasOne(d => d.User).WithMany(p => p.WishLists)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
