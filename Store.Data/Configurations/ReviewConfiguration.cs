using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Review");

        builder.HasIndex(e => e.UserId, "IX_Reviews_Review_UserId");

        builder.Property(e => e.EntityTypeId).HasMaxLength(450);
        builder.Property(e => e.ReviewerName).HasMaxLength(450);
        builder.Property(e => e.Title).HasMaxLength(450);

        builder.HasOne(d => d.User).WithMany(p => p.Reviews)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
