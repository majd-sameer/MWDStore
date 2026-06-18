using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> builder)
    {
        builder.HasKey(e => new { e.LoginProvider, e.ProviderKey });

        builder.ToTable("UserLogin");

        builder.HasIndex(e => e.UserId, "IX_Core_UserLogin_UserId");

        builder.HasOne(d => d.User).WithMany(p => p.UserLogins).HasForeignKey(d => d.UserId);
    }
}
