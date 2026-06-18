using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.ToTable("TaxRate");

        builder.HasIndex(e => e.CountryId, "IX_Tax_TaxRate_CountryId");

        builder.HasIndex(e => e.StateOrProvinceId, "IX_Tax_TaxRate_StateOrProvinceId");

        builder.HasIndex(e => e.TaxClassId, "IX_Tax_TaxRate_TaxClassId");

        builder.Property(e => e.Rate).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.ZipCode).HasMaxLength(450);

        builder.HasOne(d => d.Country).WithMany(p => p.TaxRates).HasForeignKey(d => d.CountryId);

        builder.HasOne(d => d.StateOrProvince).WithMany(p => p.TaxRates).HasForeignKey(d => d.StateOrProvinceId);

        builder.HasOne(d => d.TaxClass).WithMany(p => p.TaxRates)
            .HasForeignKey(d => d.TaxClassId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
