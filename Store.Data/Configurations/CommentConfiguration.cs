using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comment");

        builder.HasIndex(e => e.ParentId, "IX_Comments_Comment_ParentId");

        builder.HasIndex(e => e.UserId, "IX_Comments_Comment_UserId");

        builder.Property(e => e.CommenterName).HasMaxLength(450);
        builder.Property(e => e.EntityTypeId).HasMaxLength(450);

        builder.HasOne(d => d.Parent).WithMany(p => p.InverseParent).HasForeignKey(d => d.ParentId);

        builder.HasOne(d => d.User).WithMany(p => p.Comments)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
