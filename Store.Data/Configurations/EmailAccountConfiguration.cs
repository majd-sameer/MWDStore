using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class EmailAccountConfiguration : IEntityTypeConfiguration<EmailAccount>
{
    public void Configure(EntityTypeBuilder<EmailAccount> builder)
    {
        builder.ToTable("EmailAccount");

        builder.Property(e => e.Host).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Username).HasMaxLength(255);
        builder.Property(e => e.Password).HasMaxLength(255);
        builder.Property(e => e.Email).HasMaxLength(255).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(255).IsRequired();
    }
}
