using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class PaymentProviderConfiguration : IEntityTypeConfiguration<PaymentProvider>
{
    public void Configure(EntityTypeBuilder<PaymentProvider> builder)
    {
        builder.ToTable("PaymentProvider");

        builder.Property(e => e.ConfigureUrl).HasMaxLength(450);
        builder.Property(e => e.LandingViewComponentName).HasMaxLength(450);
        builder.Property(e => e.Name).HasMaxLength(450);
    }
}
