using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductAttributeGroupConfiguration : IEntityTypeConfiguration<ProductAttributeGroup>
{
    public void Configure(EntityTypeBuilder<ProductAttributeGroup> builder)
    {
        builder.ToTable("ProductAttributeGroup");

        builder.Property(e => e.Name).HasMaxLength(450);
    }
}
