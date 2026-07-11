using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ContentBlockConfiguration : IEntityTypeConfiguration<ContentBlock>
{
    public void Configure(EntityTypeBuilder<ContentBlock> builder)
    {
        builder.ToTable("ContentBlock");

        builder.HasIndex(e => e.Key).IsUnique();

        builder.Property(e => e.Key).HasMaxLength(200).IsRequired();
        builder.OwnsLocalized(e => e.Title, "Title", 500);
        builder.OwnsLocalized(e => e.Text, "Text");
        builder.OwnsLocalized(e => e.LinkText, "LinkText", 200);
        builder.Property(e => e.ImageUrl).HasMaxLength(1000);
        builder.Property(e => e.LinkUrl).HasMaxLength(1000);
    }
}
