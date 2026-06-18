using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class UserClaimConfiguration : IEntityTypeConfiguration<UserClaim>
{
    public void Configure(EntityTypeBuilder<UserClaim> builder)
    {
        builder.ToTable("UserClaim");

        builder.HasIndex(e => e.UserId, "IX_Core_UserClaim_UserId");

        builder.HasOne(d => d.User).WithMany(p => p.UserClaims).HasForeignKey(d => d.UserId);
    }
}
