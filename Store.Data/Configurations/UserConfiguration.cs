using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User");

        // EmailIndex (NormalizedEmail) and UserNameIndex (unique NormalizedUserName) are configured by
        // IdentityDbContext's base mapping with the same names/definitions; only the custom indexes remain here.
        builder.HasIndex(e => e.DefaultBillingAddressId, "IX_Core_User_DefaultBillingAddressId");

        builder.HasIndex(e => e.DefaultShippingAddressId, "IX_Core_User_DefaultShippingAddressId");

        builder.HasIndex(e => e.VendorId, "IX_Core_User_VendorId");

        builder.Property(e => e.Culture).HasMaxLength(450);
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.FullName).HasMaxLength(450);
        builder.Property(e => e.NormalizedEmail).HasMaxLength(256);
        builder.Property(e => e.NormalizedUserName).HasMaxLength(256);
        builder.Property(e => e.UserName).HasMaxLength(256);

        builder.HasOne(d => d.DefaultBillingAddress).WithMany(p => p.UserDefaultBillingAddresses).HasForeignKey(d => d.DefaultBillingAddressId);

        builder.HasOne(d => d.DefaultShippingAddress).WithMany(p => p.UserDefaultShippingAddresses).HasForeignKey(d => d.DefaultShippingAddressId);

        builder.HasOne(d => d.Vendor).WithMany(p => p.Users).HasForeignKey(d => d.VendorId);

        builder.HasMany(d => d.CustomerGroups).WithMany(p => p.Users)
            .UsingEntity<Dictionary<string, object>>(
                "CustomerGroupUser",
                r => r.HasOne<CustomerGroup>().WithMany().HasForeignKey("CustomerGroupId"),
                l => l.HasOne<User>().WithMany().HasForeignKey("UserId"),
                j =>
                {
                    j.HasKey("UserId", "CustomerGroupId");
                    j.ToTable("CustomerGroupUser");
                    j.HasIndex(new[] { "CustomerGroupId" }, "IX_Core_CustomerGroupUser_CustomerGroupId");
                });

        // The user-role relationship is mapped explicitly via the UserRole entity (see UserRoleConfiguration).
    }
}
