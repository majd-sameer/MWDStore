using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ContentBlockConfiguration : IEntityTypeConfiguration<ContentBlock>
{
    public void Configure(EntityTypeBuilder<ContentBlock> builder)
    {
        builder.ToTable("ContentBlock");

        builder.Property(e => e.PageKey).HasMaxLength(64);
        builder.Property(e => e.SectionKey).HasMaxLength(64);
        builder.Property(e => e.BlockKey).HasMaxLength(128);
        builder.Property(e => e.Type).HasMaxLength(32);
        builder.Property(e => e.Value).HasColumnType("nvarchar(max)");
        builder.Property(e => e.LinkUrl).HasMaxLength(1024);

        builder.HasIndex(e => e.PageKey, "IX_Cms_ContentBlock_PageKey");

        builder.HasIndex(e => new { e.PageKey, e.SectionKey, e.BlockKey }, "UX_Cms_ContentBlock_Key")
            .IsUnique();

        builder.HasOne(d => d.Medium).WithMany()
            .HasForeignKey(d => d.MediumId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
