using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class UserAddressConfiguration : IEntityTypeConfiguration<UserAddress>
{
    public void Configure(EntityTypeBuilder<UserAddress> builder)
    {
        builder.ToTable("UserAddress");

        builder.HasIndex(e => e.AddressId, "IX_Core_UserAddress_AddressId");

        builder.HasIndex(e => e.UserId, "IX_Core_UserAddress_UserId");

        builder.HasOne(d => d.Address).WithMany(p => p.UserAddresses)
            .HasForeignKey(d => d.AddressId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.User).WithMany(p => p.UserAddresses)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
