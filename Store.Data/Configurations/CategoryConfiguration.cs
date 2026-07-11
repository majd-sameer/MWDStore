using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Category");

        builder.HasIndex(e => e.ParentId, "IX_Catalog_Category_ParentId");

        builder.HasIndex(e => e.ThumbnailImageId, "IX_Catalog_Category_ThumbnailImageId");

        builder.OwnsLocalized(e => e.Name, "Name", 450, required: true);
        builder.OwnsLocalized(e => e.Description, "Description");
        builder.OwnsLocalized(e => e.MetaTitle, "MetaTitle", 450);
        builder.OwnsLocalized(e => e.MetaKeywords, "MetaKeywords", 450);
        builder.OwnsLocalized(e => e.MetaDescription, "MetaDescription");
        builder.Property(e => e.Slug).HasMaxLength(450);

        builder.HasOne(d => d.Parent).WithMany(p => p.InverseParent).HasForeignKey(d => d.ParentId);

        builder.HasOne(d => d.ThumbnailImage).WithMany(p => p.Categories).HasForeignKey(d => d.ThumbnailImageId);
    }
}
