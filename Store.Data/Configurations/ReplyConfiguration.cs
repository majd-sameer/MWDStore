using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ReplyConfiguration : IEntityTypeConfiguration<Reply>
{
    public void Configure(EntityTypeBuilder<Reply> builder)
    {
        builder.ToTable("Reply");

        builder.HasIndex(e => e.ReviewId, "IX_Reviews_Reply_ReviewId");

        builder.HasIndex(e => e.UserId, "IX_Reviews_Reply_UserId");

        builder.Property(e => e.ReplierName).HasMaxLength(450);

        builder.HasOne(d => d.Review).WithMany(p => p.Replies)
            .HasForeignKey(d => d.ReviewId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.User).WithMany(p => p.Replies)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
