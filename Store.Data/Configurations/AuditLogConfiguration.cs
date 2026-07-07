using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog");

        builder.Property(e => e.UserName).HasMaxLength(256);
        builder.Property(e => e.Role).HasMaxLength(64);
        builder.Property(e => e.Action).HasMaxLength(64);
        builder.Property(e => e.EntityType).HasMaxLength(128);
        builder.Property(e => e.EntityName).HasMaxLength(512);
        builder.Property(e => e.Area).HasMaxLength(64);
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.CorrelationId).HasMaxLength(128);
        builder.Property(e => e.OldValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.NewValuesJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(e => e.CreatedOn, "IX_System_AuditLog_CreatedOn");
        builder.HasIndex(e => new { e.EntityType, e.EntityId }, "IX_System_AuditLog_Entity");
        builder.HasIndex(e => e.UserId, "IX_System_AuditLog_UserId");
    }
}
