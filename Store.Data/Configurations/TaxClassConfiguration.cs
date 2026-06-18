using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class TaxClassConfiguration : IEntityTypeConfiguration<TaxClass>
{
    public void Configure(EntityTypeBuilder<TaxClass> builder)
    {
        builder.ToTable("TaxClass");

        builder.Property(e => e.Name).HasMaxLength(450);
    }
}
