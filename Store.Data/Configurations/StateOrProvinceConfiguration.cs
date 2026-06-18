using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class StateOrProvinceConfiguration : IEntityTypeConfiguration<StateOrProvince>
{
    public void Configure(EntityTypeBuilder<StateOrProvince> builder)
    {
        builder.ToTable("StateOrProvince");

        builder.HasIndex(e => e.CountryId, "IX_Core_StateOrProvince_CountryId");

        builder.Property(e => e.Code).HasMaxLength(450);
        builder.Property(e => e.Name).HasMaxLength(450);
        builder.Property(e => e.Type).HasMaxLength(450);

        builder.HasOne(d => d.Country).WithMany(p => p.StateOrProvinces).HasForeignKey(d => d.CountryId);
    }
}
