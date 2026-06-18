using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("Page");

        builder.HasIndex(e => e.CreatedById, "IX_Cms_Page_CreatedById");

        builder.HasIndex(e => e.LatestUpdatedById, "IX_Cms_Page_LatestUpdatedById");

        builder.Property(e => e.MetaKeywords).HasMaxLength(450);
        builder.Property(e => e.MetaTitle).HasMaxLength(450);
        builder.Property(e => e.Name).HasMaxLength(450);
        builder.Property(e => e.Slug).HasMaxLength(450);

        builder.HasOne(d => d.CreatedBy).WithMany(p => p.PageCreatedBies)
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.LatestUpdatedBy).WithMany(p => p.PageLatestUpdatedBies)
            .HasForeignKey(d => d.LatestUpdatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
