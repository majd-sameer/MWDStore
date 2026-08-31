using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
{
    public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
    {
        builder.ToTable("UserRefreshToken");

        // The hash is the lookup key on every refresh, and two tokens can never share one
        // (256 bits of entropy per token), so the index is unique.
        builder.HasIndex(e => e.TokenHash, "IX_Core_UserRefreshToken_TokenHash").IsUnique();

        builder.HasIndex(e => e.UserId, "IX_Core_UserRefreshToken_UserId");

        // SHA-256 as base64 is 44 chars; sized generously in case the hash format ever widens.
        builder.Property(e => e.TokenHash).HasMaxLength(88);

        builder.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
