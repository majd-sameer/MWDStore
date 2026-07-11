using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class NewsItemConfiguration : IEntityTypeConfiguration<NewsItem>
{
    public void Configure(EntityTypeBuilder<NewsItem> builder)
    {
        builder.ToTable("NewsItem");

        builder.HasIndex(e => e.CreatedById, "IX_News_NewsItem_CreatedById");

        builder.HasIndex(e => e.LatestUpdatedById, "IX_News_NewsItem_LatestUpdatedById");

        builder.HasIndex(e => e.ThumbnailImageId, "IX_News_NewsItem_ThumbnailImageId");

        builder.OwnsLocalized(e => e.Name, "Name", 450, required: true);
        builder.OwnsLocalized(e => e.ShortContent, "ShortContent", 450);
        builder.OwnsLocalized(e => e.FullContent, "FullContent");
        // Meta* stay plain string (unlocalized), per Phase 4 scope.
        builder.Property(e => e.MetaKeywords).HasMaxLength(450);
        builder.Property(e => e.MetaTitle).HasMaxLength(450);
        builder.Property(e => e.Slug).HasMaxLength(450);

        builder.HasOne(d => d.CreatedBy).WithMany(p => p.NewsItemCreatedBies)
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.LatestUpdatedBy).WithMany(p => p.NewsItemLatestUpdatedBies)
            .HasForeignKey(d => d.LatestUpdatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.ThumbnailImage).WithMany(p => p.NewsItems).HasForeignKey(d => d.ThumbnailImageId);
    }
}
