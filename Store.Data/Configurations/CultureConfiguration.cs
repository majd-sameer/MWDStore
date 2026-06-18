using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class CultureConfiguration : IEntityTypeConfiguration<Culture>
{
    public void Configure(EntityTypeBuilder<Culture> builder)
    {
        builder.ToTable("Culture");

        builder.Property(e => e.Name).HasMaxLength(450);
    }
}
