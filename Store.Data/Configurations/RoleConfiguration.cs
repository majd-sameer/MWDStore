using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");

        // RoleNameIndex (unique NormalizedName) is configured by IdentityDbContext's base mapping with the
        // same name/definition.
        builder.Property(e => e.Name).HasMaxLength(256);
        builder.Property(e => e.NormalizedName).HasMaxLength(256);
    }
}
