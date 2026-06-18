using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class RoleClaimConfiguration : IEntityTypeConfiguration<RoleClaim>
{
    public void Configure(EntityTypeBuilder<RoleClaim> builder)
    {
        builder.ToTable("RoleClaim");

        builder.HasIndex(e => e.RoleId, "IX_Core_RoleClaim_RoleId");

        builder.HasOne(d => d.Role).WithMany(p => p.RoleClaims).HasForeignKey(d => d.RoleId);
    }
}
