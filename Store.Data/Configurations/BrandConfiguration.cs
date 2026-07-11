using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brand");

        builder.OwnsLocalized(e => e.Name, "Name", 450, required: true);
        builder.OwnsLocalized(e => e.Description, "Description");
        builder.Property(e => e.Slug).HasMaxLength(450);
    }
}
