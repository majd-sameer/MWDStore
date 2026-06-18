using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activity");

        builder.HasIndex(e => e.ActivityTypeId, "IX_ActivityLog_Activity_ActivityTypeId");

        builder.Property(e => e.EntityTypeId).HasMaxLength(450);

        builder.HasOne(d => d.ActivityType).WithMany(p => p.Activities)
            .HasForeignKey(d => d.ActivityTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
