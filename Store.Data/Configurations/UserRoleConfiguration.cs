using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasKey(e => new { e.UserId, e.RoleId });

        builder.ToTable("UserRole");

        builder.HasIndex(e => e.RoleId, "IX_Core_UserRole_RoleId");

        builder.HasOne(d => d.User).WithMany(p => p.Roles)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Role).WithMany(p => p.Users)
            .HasForeignKey(d => d.RoleId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
