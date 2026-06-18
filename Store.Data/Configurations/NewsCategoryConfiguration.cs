using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class NewsCategoryConfiguration : IEntityTypeConfiguration<NewsCategory>
{
    public void Configure(EntityTypeBuilder<NewsCategory> builder)
    {
        builder.ToTable("NewsCategory");

        builder.Property(e => e.MetaKeywords).HasMaxLength(450);
        builder.Property(e => e.MetaTitle).HasMaxLength(450);
        builder.Property(e => e.Name).HasMaxLength(450);
        builder.Property(e => e.Slug).HasMaxLength(450);

        builder.HasMany(d => d.NewsItems).WithMany(p => p.Categories)
            .UsingEntity<Dictionary<string, object>>(
                "NewsItemCategory",
                r => r.HasOne<NewsItem>().WithMany().HasForeignKey("NewsItemId"),
                l => l.HasOne<NewsCategory>().WithMany().HasForeignKey("CategoryId"),
                j =>
                {
                    j.HasKey("CategoryId", "NewsItemId");
                    j.ToTable("NewsItemCategory");
                    j.HasIndex(new[] { "NewsItemId" }, "IX_News_NewsItemCategory_NewsItemId");
                });
    }
}
