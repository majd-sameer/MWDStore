using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class MediumConfiguration : IEntityTypeConfiguration<Medium>
{
    public void Configure(EntityTypeBuilder<Medium> builder)
    {
        builder.Property(e => e.Caption).HasMaxLength(450);
        builder.Property(e => e.FileName).HasMaxLength(450);
    }
}
