using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItem");

        builder.HasIndex(e => e.EntityId, "IX_Cms_MenuItem_EntityId");

        builder.HasIndex(e => e.MenuId, "IX_Cms_MenuItem_MenuId");

        builder.HasIndex(e => e.ParentId, "IX_Cms_MenuItem_ParentId");

        builder.Property(e => e.CustomLink).HasMaxLength(450);
        builder.Property(e => e.Name).HasMaxLength(450);

        builder.HasOne(d => d.Entity).WithMany(p => p.MenuItems).HasForeignKey(d => d.EntityId);

        builder.HasOne(d => d.Menu).WithMany(p => p.MenuItems)
            .HasForeignKey(d => d.MenuId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.Parent).WithMany(p => p.InverseParent).HasForeignKey(d => d.ParentId);
    }
}
