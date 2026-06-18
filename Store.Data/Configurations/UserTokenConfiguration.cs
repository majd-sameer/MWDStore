using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

        builder.ToTable("UserToken");

        builder.HasOne(d => d.User).WithMany(p => p.UserTokens).HasForeignKey(d => d.UserId);
    }
}
